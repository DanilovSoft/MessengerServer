using Contract;
using DanilovSoft.WebSocket;
using System;
using System.Linq;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MsgPack.Serialization;
using MsgPack;
using System.Diagnostics;
using Ninject;
using System.Collections.Concurrent;
using Ninject.Activation.Blocks;
using DynamicMethodsLib;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;

namespace wRPC
{
    /// <summary>
    /// Контекст соединения Web-Сокета. Владеет соединением.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public class Context : IDisposable
    {
        #region Debug
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{{{nameof(Context)}}}, UserId = {UserId}" + "}";
        #endregion

        public StandardKernel IoC { get; }
        /// <summary>
        /// Сервер который принял текущее соединение.
        /// </summary>
        private readonly Listener _listener;
        /// <summary>
        /// Потокобезопасный словарь используемый только для чтения.
        /// Хранит все доступные контроллеры. Не учитывает регистр.
        /// </summary>
        protected readonly Dictionary<string, Type> Controllers;
        /// <summary>
        /// Объект синхронизации текущего экземпляра.
        /// </summary>
        private readonly object _syncObj = new object();
        /// <summary>
        /// Объект синхронизации для переиспользования прокси интерфейсов.
        /// </summary>
        private readonly object _proxyObj = new object();
        private readonly Dictionary<Type, object> _proxies = new Dictionary<Type, object>();
        internal readonly RequestQueue _requestQueue = new RequestQueue();
        private readonly bool _isClientConnection;
        internal MyWebSocket WebSocket { get; }
        public bool IsAuthorized { get; private set; }
        public int? UserId { get; private set; }
        /// <summary>
        /// Смежные соединения текущего пользователя.
        /// </summary>
        private UserConnections _connections;
        private bool _disposed;
        protected volatile bool _connected;

        /// <summary>
        /// Конструктор клиента.
        /// </summary>
        /// <param name="callingAssembly">Сборка в которой будет осеществляться поиск контроллеров.</param>
        internal Context(Assembly callingAssembly)
        {
            _isClientConnection = true;

            // Словарь с найденными контроллерами в вызывающей сборке.
            Controllers = GlobalVars.FindAllControllers(callingAssembly);

            // У каждого клиента свой IoC.
            IoC = new StandardKernel();

            foreach (Type controllerType in Controllers.Values)
                IoC.Bind(controllerType).ToSelf();

            WebSocket = new MyClientWebSocket();

            // Разрешает серверу доступ к контроллерам клиента.
            IsAuthorized = true;
        }

        /// <summary>
        /// Конструктор сервера.
        /// </summary>
        internal Context(MyWebSocket clientConnection, StandardKernel ioc, Listener listener)
        {
            _isClientConnection = false;
            _listener = listener;
            IoC = ioc;

            // Копируем список контроллеров сервера.
            Controllers = listener.Controllers;

            WebSocket = clientConnection;
            _connected = true;

            // Начать обработку запросов текущего пользователя.
            StartReceivingLoop(WebSocket);
        }

        public T GetProxy<T>()
        {
            var attrib = typeof(T).GetCustomAttribute<ControllerContractAttribute>(inherit: false);
            if (attrib == null)
                throw new ArgumentNullException("controllerName", $"Укажите имя контроллера или пометьте интерфейс атрибутом \"{nameof(ControllerContractAttribute)}\"");

            return GetProxy<T>(attrib.ControllerName);
        }

        public T GetProxy<T>(string controllerName)
        {
            Type type = typeof(T);
            lock (_proxyObj)
            {
                if(_proxies.TryGetValue(type, out object proxy))
                {
                    return (T)proxy;
                }

                T proxyT = TypeProxy.Create<T, InterfaceProxy>((this, controllerName));
                _proxies.Add(type, proxyT);
                return proxyT;
            }
        }

        /// <summary>
        /// Происходит при обращении к проксирующему интерфейсу.
        /// </summary>
        internal object OnProxyCall(MethodInfo targetMethod, object[] args, string controllerName)
        {
            #region CreateArgs()
            Message.Arg[] CreateArgs()
            {
                ParameterInfo[] par = targetMethod.GetParameters();
                Message.Arg[] retArgs = new Message.Arg[par.Length];

                for (int i = 0; i < par.Length; i++)
                {
                    ParameterInfo p = par[i];
                    retArgs[i] = new Message.Arg(p.Name, MessagePackObject.FromObject(args[i]));
                }
                return retArgs;
            }
            #endregion

            var request = new Message($"{controllerName}/{targetMethod.Name}")
            {
                Args = CreateArgs()
            };

            // Задача с ответом сервера.
            Task<object> taskObject = WaitResponseAsync(request);

            // Если возвращаемый тип функции — Task.
            if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
            {
                // Если у задачи есть результат.
                if (targetMethod.ReturnType.IsGenericType)
                {
                    // Тип результата задачи.
                    Type resultType = targetMethod.ReturnType.GenericTypeArguments[0];

                    // Task должен быть преобразован в Task<T>.
                    return TaskConverter.ConvertTask(taskObject, resultType);
                }

                // Если возвращаемый тип Task(без результата) то можно вернуть Task<object>.
                return taskObject;
            }
            else
            // Была вызвана синхронная функция.
            {
                using (taskObject)
                {
                    object rawResult = taskObject.GetAwaiter().GetResult();
                    return rawResult;
                }
            }
        }

        private async Task<object> WaitResponseAsync(Message request)
        {
            // Добавить в очередь запросов.
            TaskCompletionSource tcs = _requestQueue.CreateRequest();
            request.Uid = tcs.Uid;

            // Арендуем память.
            using (var rentMem = new ArrayPool(4096))
            {
                // Замена MemoryStream.
                using (var stream = new MemoryStream(rentMem.Buffer))
                {
                    // Сериализуем запрос в память.
                    GlobalVars.MessageSerializer.Pack(stream, request);

                    if (this is ClientContext clientContext)
                    {
                        // Выполнить подключение сокета если еще не подключен.
                        await clientContext.ConnectIfNeededAsync().ConfigureAwait(false);
                    }

                    var segment = new ArraySegment<byte>(rentMem.Buffer, 0, (int)stream.Position);
                    // Отправка запроса.
                    await WebSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                }
            }

            // Ожидаем результат от потока поторый читает из сокета.
            Message response = await tcs;

            response.EnsureSuccessStatusCode();

            object rawResult = response.Result.ToObject();
            return rawResult;
        }

        /// <summary>
        /// Производит авторизацию текущего подключения.
        /// </summary>
        /// <param name="userId"></param>
        /// <exception cref="RemoteException"/>
        public void Authorize(int userId)
        {
            // Функцию могут вызвать из нескольких потоков.
            lock (_syncObj)
            {
                if (!IsAuthorized)
                {
                    // Авторизуем контекст пользователя.
                    UserId = userId;
                    IsAuthorized = true;

                    // Добавляем соединение в словарь.
                    _connections = AddConnection(userId);

                    // Подпишемся на дисконнект.
                    // Событие сработает даже если соединение уже разорвано.
                    WebSocket.Disconnected += WebSocket_Disconnected;
                }
                else
                    throw new RemoteException($"You are already authorized as 'UserId: {UserId}'", ErrorCode.BadRequest);
            }
        }

        /// <summary>
        /// Потокобезопасно добавляет текущее соединение в словарь.
        /// </summary>
        private UserConnections AddConnection(int userId)
        {
            do
            {
                // Берем существующую структуру или создаем новую.
                UserConnections userConnections = _listener.Connections.GetOrAdd(userId, uid => new UserConnections(uid));

                // Может случиться так что мы взяли существующую коллекцию но её удаляют из словаря в текущий момент.
                lock (userConnections.SyncRoot) // Захватить эксклюзивный доступ.
                {
                    // Если коллекцию еще не удалили из словаря то можем безопасно добавить в неё соединение.
                    if (!userConnections.IsDestroyed)
                    {
                        userConnections.Add(this);
                        return userConnections;
                    }
                }
            } while (true);
        }

        private void WebSocket_Disconnected(object sender, EventArgs e)
        {
            // Копия на случай null.
            UserConnections cons = _connections;

            if(cons != null)
            {
                // Захватить эксклюзивный доступ.
                lock (cons.SyncRoot)
                {
                    // Текущее соединение нужно безусловно удалить.
                    if (cons.Remove(this))
                    {
                        // Если соединений больше не осталось то удалить себя из словаря.
                        if (!cons.IsDestroyed && cons.Count == 0)
                        {
                            // Использовать текущую структуру больше нельзя.
                            cons.IsDestroyed = true;

                            _listener.Connections.TryRemove(UserId.Value, out _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Вызывает запрошенный клиентом метод и возвращает результат.
        /// </summary>
        /// <exception cref="RemoteException"/>
        private async Task<object> InvokeActionAsync(Message request)
        {
            // Находим контроллер.
            Type controllerType = FindRequestedController(request, out string controllerName, out string actionName);
            if(controllerType == null)
                throw new RemoteException($"Unable to find requested controller \"{controllerName}\"", ErrorCode.ActionNotFound);

            // Ищем делегат запрашиваемой функции.
            MethodInfo method = controllerType.GetMethod(actionName);
            if (method == null)
                throw new RemoteException($"Unable to find requested action \"{request.ActionName}\"", ErrorCode.ActionNotFound);

            // Проверить доступ к функции.
            PermissionCheck(method, controllerType);

            // Блок IoC выполнит Dispose всем созданным экземплярам.
            using (IActivationBlock iocBlock = IoC.BeginBlock())
            {
                // Активируем контроллер через IoC.
                using (var controller = (Controller)iocBlock.Get(controllerType))
                {
                    // Подготавливаем контроллер.
                    controller.Context = this;
                    controller.Listener = _listener;

                    // Мапим аргументы по их именам.
                    object[] args = GetParameters(method, request);

                    // Вызов делегата.
                    object result = method.InvokeFast(controller, args);

                    if (result != null)
                    {
                        // Результатом делегата может быть Task.
                        result = await DynamicAwaiter.FromAsync(result);
                    }

                    // Результат успешно получен.
                    return result;
                }
            }
        }

        /// <summary>
        /// Проверяет доступность запрашиваемого метода пользователем.
        /// </summary>
        /// <exception cref="RemoteException"/>
        private void PermissionCheck(MethodInfo method, Type controllerType)
        {
            // Проверить доступен ли метод пользователю.
            if (IsAuthorized)
                return;

            // Разрешить если метод помечен как разрешенный для не авторизованных пользователей.
            if (Attribute.IsDefined(method, typeof(AllowAnonymousAttribute)))
                return;

            // Разрешить если контроллер помечен как разрешенный для не акторизованных пользователей.
            if (Attribute.IsDefined(controllerType, typeof(AllowAnonymousAttribute)))
                return;

            throw new RemoteException("The request requires user authentication", ErrorCode.Unauthorized);
        }

        /// <summary>
        /// Пытается найти запрашиваемый пользователем контроллер.
        /// </summary>
        private Type FindRequestedController(Message request, out string controllerName, out string actionName)
        {
            int index = request.ActionName.IndexOf('/');
            if (index == -1)
            {
                controllerName = "Home";
                actionName = request.ActionName;
            }
            else
            {
                controllerName = request.ActionName.Substring(0, index);
                actionName = request.ActionName.Substring(index + 1);
            }

            controllerName += "Controller";

            // Ищем контроллер в кэше.
            Controllers.TryGetValue(controllerName, out Type controllerType);

            return controllerType;
        }

        /// <summary>
        /// Производит маппинг аргументов запроса в соответствии с делегатом.
        /// </summary>
        private object[] GetParameters(MethodInfo method, Message request)
        {
            ParameterInfo[] par = method.GetParameters();
            object[] args = new object[par.Length];

            for (int i = 0; i < par.Length; i++)
            {
                ParameterInfo p = par[i];
                string parName = p.Name;
                args[i] = request.Args.FirstOrDefault(x => x.ParameterName.Equals(parName, StringComparison.InvariantCultureIgnoreCase))?.Value.ToObject();
            }

            return args;
        }

        /// <summary>
        /// Запускает бесконечный цикл считывающий из сокета зпросы и ответы.
        /// </summary>
        protected async void StartReceivingLoop(object state)
        {
            var ws = (MyWebSocket)state;

            // Бесконечно обрабатываем запросы пользователя.
            while (true)
            {
                Message message = null;
                Message errorResponse = null;

                // Арендуем память.
                using (var buffer = new ArrayPool(4096))
                {
                    #region Читаем запрос из сокета

                    WebSocketReceiveResult webSocketMessage;
                    try
                    {
                        webSocketMessage = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer.Buffer), CancellationToken.None);
                    }
                    catch (Exception ex)
                    // Обрыв соединения.
                    {
                        Debug.WriteLine(ex);
                        return;
                    }
                    #endregion

                    // Другая сторона закрыла соединение.
                    if(webSocketMessage.MessageType == WebSocketMessageType.Close)
                    {
                        // Завершить поток.
                        return;
                    }

                    #region Десериализуем запрос

                    using (var mem = new MemoryStream(buffer.Buffer, 0, webSocketMessage.Count))
                    {
                        try
                        {
                            message = GlobalVars.MessageSerializer.Unpack(mem);
                        }
                        catch (Exception ex)
                        // Ошибка десериализации запроса.
                        {
                            Debug.WriteLine(ex);

                            // Подготоваить ответ с ошибкой.
                            errorResponse = message.ErrorResponse($"Unable to deserialize type \"{typeof(Message).Name}\"", ErrorCode.InvalidRequestFormat);
                        }
                    }
                    #endregion
                }

                if (message.IsRequest)
                {
                    if (errorResponse == null)
                    // Запрос десериализован без ошибок.
                    {
                        // Начать выполнение запроса не блокируя обработку следующих запросов этого клиента.
                        ThreadPool.UnsafeQueueUserWorkItem(StartProcessRequestAsync, message);
                    }
                    else
                    // Произошла ошибка при разборе запроса.
                    {
                        // Отправить результат с ошибкой не блокируя обработку следующих запросов этого клиента.
                        ThreadPool.UnsafeQueueUserWorkItem(SendErrorResponse, errorResponse);
                    }
                }
                else
                {
                    _requestQueue.OnResponse(message);
                }
            }
        }

        private async void SendErrorResponse(object state)
        {
            var errorResponse = (Message)state;

            try
            {
                await SendResponseAsync(errorResponse);
            }
            catch (Exception ex)
            // Обрыв соединения.
            {
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Выполняет запрос клиента и отправляет ему результат или ошибку.
        /// </summary>
        private async void StartProcessRequestAsync(object state)
        {
            var request = (Message)state;

            // Выполнить запрос и получить инкапсулированный результат.
            Message response = await GetResponseAsync(request);

            try
            {
                // Сериализовать и отправить результат.
                await SendResponseAsync(response);
            }
            catch (Exception ex)
            // Обрыв соединения.
            {
                Debug.WriteLine(ex);

                // Ничего не предпринимаем.
                return;
            }
        }

        /// <summary>
        /// Выполняет запрос клиента и инкапсулирует результат в <see cref="Response"/>. Не бросает исключения.
        /// </summary>
        private async Task<Message> GetResponseAsync(Message request)
        {
            try
            {
                // Выполнить запрашиваемую функцию
                object result = await InvokeActionAsync(request);

                // Запрашиваемая функция выполнена успешно.
                // Подготовить возвращаемый результат.
                return OkResponse(request, result);
            }
            catch (RemoteException ex)
            // Дружелюбная ошибка.
            {
                // Подготовить результат с ошибкой.
                return request.ErrorResponse(ex);
            }
            catch (Exception ex)
            // Злая ошибка обработки запроса.
            {
                DebugOnly.Break();
                Debug.WriteLine(ex);

                // Подготовить результат с ошибкой.
                return request.ErrorResponse("Internal Server Error", ErrorCode.InternalError);
            }
        }

        private Message OkResponse(Message request, object result)
        {
            return new Message(request.Uid, MessagePackObject.FromObject(result), error: null, errorCode: null);
        }

        private async Task SendResponseAsync(Message response)
        {
            using (var arrayPool = response.Serialize(out int size))
            {
                await WebSocket.SendAsync(new ArraySegment<byte>(arrayPool.Buffer, 0, size), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
            }
        }

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                WebSocket.Dispose();
            }
        }
    }
}
