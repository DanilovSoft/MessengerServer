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
    public abstract class Context : IDisposable
    {
        public StandardKernel IoC { get; }
        /// <summary>
        /// Объект синхронизации для переиспользования прокси интерфейсов.
        /// </summary>
        private readonly object _proxyObj = new object();
        private readonly Dictionary<Type, object> _proxies = new Dictionary<Type, object>();
        private readonly RequestQueue _requestQueue = new RequestQueue();
        /// <summary>
        /// Потокобезопасный словарь используемый только для чтения.
        /// Хранит все доступные контроллеры. Не учитывает регистр.
        /// </summary>
        protected Dictionary<string, Type> Controllers;
        internal MyWebSocket WebSocket { get; set; }
        private bool _disposed;

        // ctor.
        /// <param name="callingAssembly">Сборка в которой будет осеществляться поиск контроллеров.</param>
        internal Context(Assembly callingAssembly)
        {
            // Словарь с найденными контроллерами в вызывающей сборке.
            Controllers = GlobalVars.FindAllControllers(callingAssembly);

            var settings = new Ninject.NinjectSettings() { LoadExtensions = false };
            // У каждого клиента свой IoC.
            IoC = new StandardKernel(settings);

            foreach (Type controllerType in Controllers.Values)
                IoC.Bind(controllerType).ToSelf();
        }

        /// <summary>
        /// Конструктор сервера.
        /// </summary>
        internal Context(MyWebSocket clientConnection, StandardKernel ioc)
        {
            IoC = ioc;
            WebSocket = clientConnection;
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
                    retArgs[i] = new Message.Arg(p.Name, args[i]);
                }
                return retArgs;
            }
            #endregion

            var request = new Message($"{controllerName}/{targetMethod.Name}")
            {
                Args = CreateArgs()
            };

            // Тип результата.
            Type resultType = GetActionReturnType(targetMethod);

            // Задача с ответом от удалённой стороны.
            Task<object> taskObject = ExecuteRequestAsync(request, resultType);

            // Если возвращаемый тип функции — Task.
            if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
            {
                // Если у задачи есть результат.
                if (targetMethod.ReturnType.IsGenericType)
                {
                    // Тип результата задачи.
                    //Type resultType = targetMethod.ReturnType.GenericTypeArguments[0];

                    // Task<object> должен быть преобразован в Task<T>.
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

        /// <summary>
        /// Отправляет запрос о дожидается его ответа.
        /// </summary>
        private protected async Task<object> ExecuteRequestAsync(Message request, Type resultType, bool doConnect = true)
        {
            // Добавить в очередь запросов.
            TaskCompletionSource tcs = _requestQueue.CreateRequest(request, out int uid);

            // Назначить запросу уникальный идентификатор.
            request.Uid = uid;

            // Арендуем память.
            using (var rentMem = new ArrayPool(4096))
            {
                // Замена MemoryStream.
                using (var stream = new MemoryStream(rentMem.Buffer))
                {
                    // Сериализуем запрос в память.
                    request.Serialize(stream);

                    if (doConnect && this is ClientContext clientContext)
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

            // Исключение если запрос завершен с ошибкой.
            response.EnsureSuccessStatusCode();

            // Десериализуем результат.
            object rawResult = response.Result?.ToObject(resultType);

            return rawResult;
        }

        /// <summary>
        /// Запускает бесконечный цикл считывающий из сокета зпросы и ответы.
        /// </summary>
        protected async void StartReceivingLoop(object state)
        {
            var ws = (MyWebSocket)state;
            ws.Disconnected += Ws_Disconnected;

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
                    if (webSocketMessage.MessageType == WebSocketMessageType.Close)
                    {
                        // Завершить поток.
                        return;
                    }

                    #region Десериализуем запрос

                    using (var mem = new MemoryStream(buffer.Buffer, 0, webSocketMessage.Count))
                    {
                        try
                        {
                            message = ExtensionMethods.Deserialize<Message>(mem);
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
                    if (errorResponse == null)
                    {
                        _requestQueue.OnResponse(message);
                    }
                }
            }
        }

        private void Ws_Disconnected(object sender, EventArgs e)
        {
            var ws = (MyWebSocket)sender;
            ws.Disconnected -= Ws_Disconnected;
            ws.Dispose();

            _requestQueue.OnException(new InvalidOperationException("Произошел обрыв соединения"));
        }

        /// <summary>
        /// Вызывает запрошенный метод и возвращает результат.
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
            InvokeMethodPermissionCheck(method, controllerType);

            // Блок IoC выполнит Dispose всем созданным экземплярам.
            using (IActivationBlock iocBlock = IoC.BeginBlock())
            {
                // Активируем контроллер через IoC.
                using (var controller = (Controller)iocBlock.Get(controllerType))
                {
                    // Подготавливаем контроллер.
                    BeforeInvokePrepareController(controller);

                    // Мапим аргументы по их именам.
                    object[] args = GetParameters(method, request);

                    // Вызов делегата.
                    object rawResult = method.InvokeFast(controller, args);

                    if (rawResult != null)
                    {
                        // Результатом делегата может быть Task.
                        rawResult = await DynamicAwaiter.FromAsync(rawResult);
                    }

                    Type returnType = GetActionReturnType(method);

                    // Результат успешно получен.
                    return rawResult;
                }
            }
        }

        private static Type GetActionReturnType(MethodInfo method)
        {
            // Если возвращаемый тип функции — Task.
            if (typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                // Если у задачи есть результат.
                if (method.ReturnType.IsGenericType)
                {
                    // Тип результата задачи.
                    Type resultType = method.ReturnType.GenericTypeArguments[0];

                    return resultType;
                }

                // Возвращаемый тип Task(без результата).
                return typeof(void);
            }
            else
            // Была вызвана синхронная функция.
            {
                return method.ReturnType;
            }
        }

        /// <summary>
        /// Проверяет доступность запрашиваемого метода для удаленного пользователя.
        /// </summary>
        /// <exception cref="RemoteException"/>
        protected abstract void InvokeMethodPermissionCheck(MethodInfo method, Type controllerType);
        protected abstract void BeforeInvokePrepareController(Controller controller);

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
                args[i] = request.Args.FirstOrDefault(x => x.ParameterName.Equals(parName, StringComparison.InvariantCultureIgnoreCase))?.Value.ToObject(p.ParameterType);
            }

            return args;
        }

        private async void SendErrorResponse(object state)
        {
            var errorResponse = (Message)state;

            try
            {
                await SendMessageAsync(errorResponse);
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
                await SendMessageAsync(response);
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
                object rawResult = await InvokeActionAsync(request);

                // Запрашиваемая функция выполнена успешно.
                // Подготовить возвращаемый результат.
                return OkResponse(request, rawResult);
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

        private Message OkResponse(Message request, object rawResult)
        {
            return new Message(request.Uid, rawResult, error: null, errorCode: null);
        }

        /// <summary>
        /// Сериализует сообщение и отправляет в сокет.
        /// </summary>
        private protected async Task SendMessageAsync(Message message)
        {
            using (var arrayPool = message.Serialize(out int size))
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
