using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace wRPC
{
    /// <summary>
    /// Контекст клиентского соединения.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public class ClientContext : Context
    {
        #region Debug
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{{{GetType().Name}}}, Connected = {Socket != null}" + "}";
        #endregion
        private readonly AsyncLock _asyncLock;
        private readonly Uri _uri;
        public byte[] BearerToken { get; set; }

        /// <summary>
        /// Конструктор клиента.
        /// </summary>
        /// <param name="controllersAssembly">Сборка в которой осуществляется поиск контроллеров.</param>
        /// <param name="uri">Адрес сервера.</param>
        internal ClientContext(Assembly controllersAssembly, Uri uri) : base(controllersAssembly)
        {
            _uri = uri;
            _asyncLock = new AsyncLock();
        }

        /// <summary>
        /// Событие — обрыв сокета.
        /// </summary>
        private protected override void OnDisconnect(SocketQueue socketQueue)
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
        private protected async Task<SocketQueue> ConnectIfNeededAsync()
        {
            // Копия volatile ссылки.
            SocketQueue socketQueue = Socket;

            // Fast-path.
            if (socketQueue != null)
                return socketQueue;

            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                // Копия volatile ссылки.
                socketQueue = Socket;

                // Необходима повторная проверка.
                if (socketQueue == null)
                {
                    // Новый сокет.
                    var ws = new MyClientWebSocket();

                    try
                    {
                        // Простое подключение веб-сокета.
                        await ws.ConnectAsync(_uri).ConfigureAwait(false);
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
                    bool authorized = false;
                    if (bearerTokenCopy != null)
                    {
                        try
                        {
                            authorized = await AuthorizeAsync(socketQueue, bearerTokenCopy).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        // Обрыв соединения.
                        {
                            // Оповестить об обрыве.
                            AtomicDisconnect(socketQueue, ex);
                            throw;
                        }
                    }

                    Debug.WriteIf(authorized, "Соединение успешно авторизовано");

                    // Открыть публичный доступ к этому сокету.
                    // Установка этого свойства должно быть самым последним действием.
                    Interlocked.CompareExchange(ref _socket, socketQueue, null); // Записать только если в ссылке Null.
                }
                return socketQueue;
            }
        }

        /// <summary>
        /// Отправляет специфический запрос содержащий токен авторизации. Ожидает ответ. При успешном выполнении сокет считается авторизованным.
        /// </summary>
        private async Task<bool> AuthorizeAsync(SocketQueue socketQueue, byte[] bearerToken)
        {
            var message = new Message("Auth/AuthorizeToken")
            {
                Args = new Message.Arg[]
                {
                    new Message.Arg("token", bearerToken)
                }
            };

            // Отправить запрос и получить ответ.
            object result = await ExecuteRequestAsync(message, typeof(bool), socketQueue).ConfigureAwait(false);

            return (bool)result;
        }

        protected override void BeforeInvokePrepareController(Controller controller)
        {
            var clientController = (ClientController)controller;
            clientController.Context = this;
        }

        // Серверу всегда доступны методы клиента.
        protected override void InvokeMethodPermissionCheck(MethodInfo method, Type controllerType) { }

        public override void Dispose()
        {
            base.Dispose();
            _asyncLock.Dispose();
        }
    }
}
