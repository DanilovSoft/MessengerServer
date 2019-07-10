using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace wRPC
{
    /// <summary>
    /// Контекст клиентского соединения.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class ClientConnection : Context
    {
        #region Debug

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"Connected = {Socket != null}, IsAuthorized = {IsAuthorized}" + "}";
        
        #endregion

        /// <summary>
        /// Используется для синхронизации установки соединения.
        /// </summary>
        private readonly ChannelLock _asyncLock;
        /// <summary>
        /// Адрес для подключеия к серверу.
        /// </summary>
        private readonly Uri _uri;
        /// <summary>
        /// Токен авторизации передаваемый серверу при начальном подключении.
        /// </summary>
        public byte[] BearerToken { get; set; }
        /// <summary>
        /// <see langword="true"/> если соединение авторизовано на сервере.
        /// </summary>
        public bool IsAuthorized { get; private set; }
        private Action<ServiceCollection> _iocConfigure;

        // ctor.
        /// <summary>
        /// Создаёт контекст клиентского соединения.
        /// </summary>
        public ClientConnection(Uri uri) : this(Assembly.GetCallingAssembly(), uri)
        {

        }

        /// <summary>
        /// Позволяет настроить IoC контейнер.
        /// Выполняется единожды при инициализации подключения.
        /// </summary>
        /// <param name="configure"></param>
        public void ConfigureService(Action<ServiceCollection> configure)
        {
            _iocConfigure = configure;
        }

        /// <summary>
        /// Создаёт контекст клиентского соединения.
        /// </summary>
        public ClientConnection(string host, int port) : this(Assembly.GetCallingAssembly(), new Uri($"ws://{host}:{port}"))
        {
            
        }

        /// <summary>
        /// Конструктор клиента.
        /// </summary>
        /// <param name="controllersAssembly">Сборка в которой осуществляется поиск контроллеров.</param>
        /// <param name="uri">Адрес сервера.</param>
        internal ClientConnection(Assembly controllersAssembly, Uri uri) : base(controllersAssembly)
        {
            _uri = uri;
            _asyncLock = new ChannelLock();
        }

        /// <summary>
        /// Производит предварительное подключение сокета к серверу.
        /// </summary>
        public Task ConnectAsync()
        {
            return ConnectIfNeededAsync();
        }

        /// <summary>
        /// Событие — обрыв сокета.
        /// </summary>
        private protected override void OnAtomicDisconnect(SocketQueue socketQueue)
        {
            // Установить Socket = null если в ссылке хранится экземпляр соединения в котором произошел обрыв.
            Interlocked.CompareExchange(ref _socket, null, socketQueue);
        }

        private protected override Task<SocketQueue> GetOrCreateConnectionAsync()
        {
            return ConnectIfNeededAsync();
        }

        /// <summary>
        /// Выполнить подключение сокета если еще не подключен.
        /// </summary>
        private async Task<SocketQueue> ConnectIfNeededAsync()
        {
            // Копия volatile ссылки.
            SocketQueue socketQueue = Socket;

            // Fast-path.
            if (socketQueue != null)
                return socketQueue;

            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                // Копия volatile ссылки.
                socketQueue = _socket;

                // Необходима повторная проверка.
                if (socketQueue == null)
                {
                    if (ServiceProvider == null)
                    {
                        var ioc = new ServiceCollection();
                        _iocConfigure?.Invoke(ioc);
                        ConfigureIoC(ioc);
                    }

                    // Новый сокет.
                    var ws = new MyClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                    try
                    {
                        // Простое подключение веб-сокета.
                        await ws.ConnectAsync(_uri, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    // Не удалось подключиться (сервер не запущен?).
                    {
                        ws.Dispose();
                        throw;
                    }

                    // Управляемая обвертка для сокета.
                    socketQueue = new SocketQueue(ws);

                    // Начать бесконечное чтение из сокета.
                    StartReceivingLoop(socketQueue);

                    // Копируем ссылку на публичный токен.
                    byte[] bearerTokenCopy = BearerToken;

                    // Если токен установлен то отправить его на сервер что-бы авторизовать текущее подключение.
                    if (bearerTokenCopy != null)
                    {
                        try
                        {
                            IsAuthorized = await AuthorizeAsync(socketQueue, bearerTokenCopy).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        // Обрыв соединения.
                        {
                            // Оповестить об обрыве.
                            AtomicDisconnect(socketQueue, ex);
                            throw;
                        }
                    }

                    Debug.WriteIf(IsAuthorized, "Соединение успешно авторизовано");

                    // Открыть публичный доступ к этому сокету.
                    // Установка этого свойства должно быть самым последним действием.
                    Interlocked.CompareExchange(ref _socket, socketQueue, null); // Записать только если в ссылке Null.
                }
                return socketQueue;
            }
        }

        /// <summary>
        /// Отправляет специфический запрос содержащий токен авторизации. Ожидает ответ.
        /// </summary>
        private async Task<bool> AuthorizeAsync(SocketQueue socketQueue, byte[] bearerToken)
        {
            // Запрос на авторизацию по токену.
            var requestToSend = Message.CreateRequest("Auth/AuthorizeToken", new Arg[] { new Arg("token", bearerToken) });
            
            // Отправить запрос и получить ответ.
            object result = await ExecuteRequestAsync(requestToSend, returnType: typeof(bool), socketQueue).ConfigureAwait(false);

            return (bool)result;
        }

        protected override void BeforeInvokePrepareController(Controller controller)
        {
            var clientController = (ClientController)controller;
            clientController.Context = this;
        }

        // Серверу всегда доступны методы клиента.
        protected override void InvokeMethodPermissionCheck(MethodInfo method, Type controllerType) { }
    }
}
