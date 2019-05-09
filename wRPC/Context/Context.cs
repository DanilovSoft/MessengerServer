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
using System.Collections.Concurrent;
using DynamicMethodsLib;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace wRPC
{
    /// <summary>
    /// Контекст соединения Web-Сокета. Владеет соединением.
    /// </summary>
    public abstract class Context : IDisposable
    {
        private const string ProtocolErrorMessage = "Произошла ошибка десериализации ответа от удалённой стороны," +
                            " поэтому соединение было аварийно закрыто.";
        public ServiceCollection IoC { get; }
        /// <summary>
        /// Объект синхронизации для создания прокси из интерфейсов.
        /// </summary>
        private readonly object _proxyObj = new object();
        /// <summary>
        /// Содержит прокси созданные из интерфейсов.
        /// </summary>
        private readonly Dictionary<Type, object> _proxies = new Dictionary<Type, object>();
        /// <summary>
        /// Потокобезопасный словарь используемый только для чтения.
        /// Хранит все доступные контроллеры. Не учитывает регистр.
        /// </summary>
        protected Dictionary<string, Type> Controllers;

        private volatile SocketQueue _socket;
        /// <summary>
        /// Является <see langword="volatile"/>.
        /// </summary>
        private protected SocketQueue Socket { get => _socket; set => _socket = value; }
        private bool _disposed;

        /// <summary>
        /// Конструктор клиента.
        /// </summary>
        /// <param name="controllersAssembly">Сборка в которой будет осеществляться поиск контроллеров.</param>
        internal Context(Assembly controllersAssembly)
        {
            // Сборка с контроллерами не должна быть текущей сборкой.
            Debug.Assert(controllersAssembly != Assembly.GetExecutingAssembly());

            // Словарь с найденными контроллерами в вызывающей сборке.
            Controllers = GlobalVars.FindAllControllers(controllersAssembly);

            // У каждого клиента свой IoC.
            IoC = new ServiceCollection();

            // Добавим в IoC все контроллеры сборки.
            foreach (Type controllerType in Controllers.Values)
                IoC.AddScoped(controllerType);
        }

        /// <summary>
        /// Конструктор сервера.
        /// </summary>
        internal Context(MyWebSocket clientConnection, ServiceCollection ioc)
        {
            IoC = ioc;

            // У сервера сокет всегда подключен и переподключаться не может.
            Socket = new SocketQueue(clientConnection);
        }

        /// <summary>
        /// Создает прокси к методам удалённой стороны на основе интерфейса.
        /// </summary>
        public T GetProxy<T>()
        {
            var attrib = typeof(T).GetCustomAttribute<ControllerContractAttribute>(inherit: false);
            if (attrib == null)
                throw new ArgumentNullException("controllerName", $"Укажите имя контроллера или пометьте интерфейс атрибутом \"{nameof(ControllerContractAttribute)}\"");

            return GetProxy<T>(attrib.ControllerName);
        }

        /// <summary>
        /// Создает прокси к методам удалённой стороны на основе интерфейса.
        /// </summary>
        /// <param name="controllerName">Имя контроллера на удалённой стороне к которому применяется текущий интерфейс <see cref="{T}"/>.</param>
        public T GetProxy<T>(string controllerName)
        {
            Type interfaceType = typeof(T);
            lock (_proxyObj)
            {
                if(_proxies.TryGetValue(interfaceType, out object proxy))
                {
                    return (T)proxy;
                }

                T proxyT = TypeProxy.Create<T, InterfaceProxy>((this, controllerName));
                _proxies.Add(interfaceType, proxyT);
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
            Task<object> taskObject = ExecuteRequestAsync(request, resultType, socketQueue: null);

            // Если возвращаемый тип функции — Task.
            if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
            {
                // Если у задачи есть результат.
                if (targetMethod.ReturnType.IsGenericType)
                {
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
        /// Создает подключение или возвращает уже подготовленное соединение.
        /// </summary>
        /// <returns></returns>
        private protected abstract Task<SocketQueue> GetOrCreateConnectionAsync();
        private protected abstract void OnDisconnect();

        /// <summary>
        /// Отправляет запрос о ожидает его ответ.
        /// </summary>
        /// <param name="resultType">Тип в который будет десериализован результат запроса.</param>
        private protected async Task<object> ExecuteRequestAsync(Message request, Type resultType, SocketQueue socketQueue)
        {
            TaskCompletionSource tcs;

            // Арендуем память.
            using (var rentMem = new ArrayPool(4096))
            {
                // Замена MemoryStream.
                using (var stream = new MemoryStream(rentMem.Buffer))
                {
                    // Когда вызывает клиент то установить соединение или взять существующее.
                    if (socketQueue == null)
                    {
                        // Никогда не вызывается серверным контекстом.
                        socketQueue = await GetOrCreateConnectionAsync().ConfigureAwait(false);
                    }

                    // Добавить в очередь запросов.
                    tcs = socketQueue.RequestQueue.CreateRequest(request, out int uid);

                    // Назначить запросу уникальный идентификатор.
                    request.Uid = uid;

                    // Сериализуем запрос в память.
                    request.Serialize(stream);

                    var segment = new ArraySegment<byte>(rentMem.Buffer, 0, (int)stream.Position);

                    // Отправка запроса.
                    await socketQueue.WebSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                }
            } // Вернуть память.

            // Ожидаем результат от потока поторый читает из сокета.
            Message response = await tcs;

            // Исключение если запрос завершен с ошибкой.
            response.EnsureSuccessStatusCode();

            // Десериализуем результат.
            object rawResult = response.Result?.ToObject(resultType);

            return rawResult;
        }

        /// <summary>
        /// Запускает бесконечный цикл, в фоновом потоке, считывающий из сокета зпросы и ответы.
        /// </summary>
        private protected void StartReceivingLoop(SocketQueue socketQueue_)
        {
            ThreadPool.UnsafeQueueUserWorkItem(async state => 
            {
                var socketQueue = (SocketQueue)state;

                // Бесконечно обрабатываем сообщения сокета.
                while (true)
                {
                    Message message = null;
                    Exception deserializationException = null;

                    // Арендуем память на фрейм вебсокета.
                    using (var buffer = new ArrayPool(4096))
                    {
                        #region Читаем фрейм веб-сокета.

                        WebSocketReceiveResult webSocketMessage;
                        try
                        {
                            // Читаем фрейм веб-сокета.
                            webSocketMessage = await socketQueue.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer.Buffer), CancellationToken.None);
                        }
                        catch (Exception ex)
                        // Обрыв соединения.
                        {
                            AtomicDisconnect(socketQueue, ex);

                            // Завершить поток.
                            return;
                        }
                        #endregion

                        // Другая сторона закрыла соединение.
                        if (webSocketMessage.MessageType == WebSocketMessageType.Close)
                        {
                            // Сформировать причину закрытия соединения.
                            string exceptionMessage = GetMessageFromCloseFrame(webSocketMessage);

                            // Сообщить потокам что удалённая сторона выполнила закрытие соединения.
                            var socketClosedException = new SocketClosedException(exceptionMessage);

                            AtomicDisconnect(socketQueue, socketClosedException);

                            // Завершить поток.
                            return;
                        }

                        #region Десериализуем фрейм веб-сокета в сообщение протокола.

                        using (var mem = new MemoryStream(buffer.Buffer, 0, webSocketMessage.Count))
                        {
                            try
                            {
                                message = ExtensionMethods.Deserialize<Message>(mem);
                            }
                            catch (Exception ex)
                            // Ошибка десериализации сообщения.
                            {
                                // Подготовить ошибку для дальнейшей обработки.
                                deserializationException = ex;
                            }
                        }
                        #endregion
                    }

                    if (message.IsRequest)
                    // Получен запрос.
                    {
                        if (deserializationException == null)
                        // Запрос десериализован без ошибок.
                        {
                            // Начать выполнение запроса отдельным потоком.
                            StartProcessRequestAsync(socketQueue, message);
                        }
                        else
                        // Произошла ошибка при разборе запроса.
                        {
                            // Подготоваить ответ с ошибкой.
                            Message errorResponse = message.ErrorResponse($"Unable to deserialize type \"{nameof(Message)}\"", ErrorCode.InvalidRequestFormat);

                            // Начать отправку результата с ошибкой отдельным потоком.
                            StartSendErrorResponse(socketQueue, errorResponse);
                        }
                    }
                    else
                    // Получен ответ на запрос.
                    {
                        if (deserializationException == null)
                        {
                            // Передать ответ ожидающему потоку.
                            socketQueue.RequestQueue.OnResponse(message);
                        }
                        else
                        // Произошла ошибка при десериализации ответа, необходимо аварийно закрыть соединение.
                        {
                            var protocolErrorException = new ProtocolErrorException(ProtocolErrorMessage, deserializationException);

                            // Сообщить потокам что обрыв произошел по вине удалённой стороны.
                            socketQueue.RequestQueue.OnDisconnect(protocolErrorException);

                            try
                            {
                                // Отключаемся от сокета с небольшим таймаутом.
                                using (var cts = new CancellationTokenSource(3000))
                                    await socketQueue.WebSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Ошибка десериализации ответа", cts.Token);
                            }
                            catch (Exception ex)
                            // Злой обрыв соединения.
                            {
                                AtomicDisconnect(socketQueue, ex);

                                // Завершить поток.
                                return;
                            }

                            // Освободить соединение.
                            AtomicDisconnect(socketQueue, protocolErrorException);

                            // Завершить поток.
                            return;
                        }
                    }
                }
            }, state: socketQueue_);
        }

        /// <summary>
        /// Потокобезопасно освобождает ресурсы соединения. Вызывается при обрыве соединения.
        /// </summary>
        /// <param name="exception">Возможная причина обрыва соединения.</param>
        private protected void AtomicDisconnect(SocketQueue socketQueue, Exception exception)
        {
            if(socketQueue.TryOwn())
            {
                // Передать исключение всем ожидающим потокам.
                socketQueue.RequestQueue.OnDisconnect(exception);

                socketQueue.Dispose();

                OnDisconnect();
            }
        }

        /// <summary>
        /// Формирует сообщение ошибки из фрейма веб-сокета информирующем о закрытии соединения.
        /// </summary>
        private string GetMessageFromCloseFrame(WebSocketReceiveResult webSocketMessage)
        {
            string exceptionMessage = null;
            if (webSocketMessage.CloseStatus != null)
            {
                exceptionMessage = $"CloseStatus: {webSocketMessage.CloseStatus.ToString()}";

                if (!string.IsNullOrEmpty(webSocketMessage.CloseStatusDescription))
                {
                    exceptionMessage += $", Description: \"{webSocketMessage.CloseStatusDescription}\"";
                }
            }
            else if (!string.IsNullOrEmpty(webSocketMessage.CloseStatusDescription))
            {
                exceptionMessage = $"Description: \"{webSocketMessage.CloseStatusDescription}\"";
            }

            if (exceptionMessage == null)
                exceptionMessage = "Удалённая сторона закрыла соединение без объяснения причины.";

            return exceptionMessage;
        }

        /// <summary>
        /// Вызывает запрошенный метод контроллера и возвращает результат.
        /// </summary>
        /// <exception cref="RemoteException"/>
        private async Task<object> InvokeControllerAsync(Message request)
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
            using (ServiceProvider block = IoC.BuildServiceProvider())
            {
                // Активируем контроллер через IoC.
                using (var controller = (Controller)block.GetRequiredService(controllerType))
                {
                    // Подготавливаем контроллер.
                    BeforeInvokePrepareController(controller);

                    // Мапим аргументы по их именам.
                    object[] args = GetParameters(method, request);

                    // Вызов делегата.
                    object controllerResult = method.InvokeFast(controller, args);

                    if (controllerResult != null)
                    {
                        // Извлекает результат из Task'а.
                        controllerResult = await DynamicAwaiter.WaitAsync(controllerResult);
                    }

                    // Результат успешно получен.
                    return controllerResult;
                }
            }
        }

        /// <summary>
        /// Возвращает инкапсулированный в Task тип результата функции.
        /// </summary>
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

        /// <summary>
        /// Отправляет результат с ошибкой в новом потоке.
        /// </summary>
        /// <param name="message_"></param>
        private void StartSendErrorResponse(SocketQueue socketQueue_, Message message_)
        {
            ThreadPool.UnsafeQueueUserWorkItem(async state =>
            {
                var tuple = ((SocketQueue socketQueue, Message errorMessage))state;

                // Сериализовать и отправить результат.
                await SendMessageAsync(tuple.socketQueue, tuple.errorMessage);

            }, state: (socketQueue_, message_)); // Без замыкания.
        }

        /// <summary>
        /// В новом потоке выполняет запрос клиента и отправляет ему результат или ошибку.
        /// </summary>
        private void StartProcessRequestAsync(SocketQueue socketQueue_, Message message_)
        {
            ThreadPool.UnsafeQueueUserWorkItem(async state =>
            {
                var tuple = ((SocketQueue socketQueue, Message message))state;

                // Выполнить запрос и создать сообщение с результатом.
                Message response = await GetResponseAsync(tuple.message);

                // Сериализовать и отправить результат.
                await SendMessageAsync(tuple.socketQueue, response);

            }, state: (socketQueue_, message_));
        }

        /// <summary>
        /// Выполняет запрос клиента и инкапсулирует результат в <see cref="Response"/>. Не бросает исключения.
        /// </summary>
        private async Task<Message> GetResponseAsync(Message request)
        {
            try
            {
                // Выполнить запрашиваемую функцию
                object rawResult = await InvokeControllerAsync(request);

                // Запрашиваемая функция выполнена успешно.
                // Подготовить возвращаемый результат.
                return OkResponse(request, rawResult);
            }
            catch (RemoteException ex)
            // Дружелюбная ошибка.
            {
                // Вернуть результат с ошибкой.
                return request.ErrorResponse(ex);
            }
            catch (Exception ex)
            // Злая ошибка обработки запроса. Ошибка 500.
            {
                DebugOnly.Break();
                Debug.WriteLine(ex);

                // Вернуть результат с ошибкой.
                return request.ErrorResponse("Internal Server Error", ErrorCode.InternalError);
            }
        }

        /// <summary>
        /// Подготавливает ответ на запрос и копирует идентификатор из запроса.
        /// </summary>
        private Message OkResponse(Message request, object rawResult)
        {
            // Конструктор ответа.
            return new Message(request.Uid, rawResult, error: null, errorCode: null);
        }

        /// <summary>
        /// Сериализует сообщение и отправляет в сокет. Вызывать только из фонового потока.
        /// </summary>
        private protected async Task SendMessageAsync(SocketQueue socketQueue, Message message)
        {
            using (var arrayPool = message.Serialize(out int size))
            {
                try
                {
                    await socketQueue.WebSocket.SendAsync(new ArraySegment<byte>(arrayPool.Buffer, 0, size), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
                }
                catch (Exception ex)
                // Обрыв соединения.
                {
                    AtomicDisconnect(socketQueue, ex);
                }   
            }
        }

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                SocketQueue socket = Socket;
                if (socket != null)
                    AtomicDisconnect(socket, new ObjectDisposedException(GetType().Name));
            }
        }
    }
}
