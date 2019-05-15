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
        /// <summary>
        /// Максимальный размер фрейма который может использовать веб-сокет.
        /// </summary>
        private const int WebSocketFrameMaxSize = 4096;
        private const string ProtocolErrorMessage = "Произошла ошибка десериализации сообщения от удалённой стороны," +
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
        private protected volatile SocketQueue _socket;
        /// <summary>
        /// Является <see langword="volatile"/>.
        /// </summary>
        private protected SocketQueue Socket { get => _socket; set => _socket = value; }
        /// <summary>
        /// Токен отмены связанный с текущим соединением. Позволяет прервать действие контроллера при обрыве соединения.
        /// </summary>
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        /// <summary>
        /// Токен отмены связанный с текущим соединением.
        /// </summary>
        public CancellationToken CancellationToken => _cts.Token;
        /// <summary>
        /// Отправка сообщения <see cref="Message"/> должна выполняться только с захватом этой блокировки.
        /// </summary>
        private readonly AsyncLock _sendMessageLock = new AsyncLock();
        private int _disposed;
        private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

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
            ThrowIfDisposed();

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

            // Тип результата инкапсулированный в Task<T>.
            Type resultType = GetActionReturnType(targetMethod);

            // Задача с ответом от удалённой стороны.
            Task<object> taskObject = ExecuteRequestAsync(request, resultType, Socket);

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
                // При синхронном ожидании Task нужно выполнять Dispose.
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
        private protected abstract void OnDisconnect(SocketQueue socketQueue);

        /// <summary>
        /// Отправляет запрос о ожидает его ответ.
        /// </summary>
        /// <param name="resultType">Тип в который будет десериализован результат запроса.</param>
        private protected async Task<object> ExecuteRequestAsync(Message request, Type resultType, SocketQueue socketQueue)
        {
            TaskCompletionSource tcs;

            // Арендуем память.
            //using (var mem = new MemoryPoolStream(128))
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
                //request.Serialize(mem);

                //byte[] pooledBuffer = mem.GetBuffer();
                //var segment = new ArraySegment<byte>(pooledBuffer, 0, (int)mem.Position);

                // Отправка запроса.
                await SendMessageAsync(socketQueue, request);
                //await socketQueue.WebSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
            } // Вернуть память.

            // Ожидаем результат от потока поторый читает из сокета.
            Message response = await tcs;

            // Исключение если запрос завершен с ошибкой.
            response.EnsureSuccessStatusCode();

            // Десериализуем результат.
            //object rawResult = ExtensionMethods.Deserialize(response.Result, resultType);

            return response.Result;
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
                    Header header = null;
                    Message message = null;
                    Exception deserializationException = null;

                    // Арендуем память на фрейм вебсокета.
                    using (var mem = new MemoryPoolStream(128))
                    {
                        WebSocketReceiveResult webSocketMessage;
                        using (var frameMem = new MemoryPoolStream(WebSocketFrameMaxSize))
                        {
                            byte[] pooledBuffer = frameMem.GetBuffer();
                            var segment = new ArraySegment<byte>(pooledBuffer);
                            do
                            {
                                #region Читаем фрейм веб-сокета.

                                try
                                {
                                    // Читаем фрейм веб-сокета.
                                    webSocketMessage = await socketQueue.WebSocket.ReceiveAsync(segment, CancellationToken.None);
                                }
                                catch (Exception ex)
                                // Обрыв соединения.
                                {
                                    // Оповестить об обрыве.
                                    AtomicDisconnect(socketQueue, ex);

                                    // Завершить поток.
                                    return;
                                }

                                #endregion

                                #region Проверка на Close.

                                // Другая сторона закрыла соединение.
                                if (webSocketMessage.MessageType == WebSocketMessageType.Close)
                                {
                                    // Сформировать причину закрытия соединения.
                                    string exceptionMessage = GetMessageFromCloseFrame(webSocketMessage);

                                    // Сообщить потокам что удалённая сторона выполнила закрытие соединения.
                                    var socketClosedException = new SocketClosedException(exceptionMessage);

                                    // Оповестить об обрыве.
                                    AtomicDisconnect(socketQueue, socketClosedException);

                                    // Завершить поток.
                                    return;
                                }
                                #endregion

                                // Копирование фрейма в MemoryStream.
                                mem.Write(pooledBuffer, 0, webSocketMessage.Count);

                            } while (!webSocketMessage.EndOfMessage);
                        }

                        #region Десериализуем фрейм веб-сокета в сообщение протокола.

                        mem.Position = 0;
                        try
                        {
                            header = Header.Deserialize(mem);
                        }
                        catch (Exception ex)
                        // Ошибка десериализации сообщения.
                        {
                            // Подготовить ошибку для дальнейшей обработки.
                            deserializationException = ex;
                        }

                        message = ExtensionMethods.Deserialize<Message>(mem);

                        #endregion
                    }

                    if (deserializationException == null)
                    // Сообщение десериализовано без ошибок.
                    {
                        if (message.IsRequest)
                        // Получен запрос.
                        {
                            // Начать выполнение запроса в отдельном потоке.
                            StartProcessRequestAsync(socketQueue, message);
                        }
                        else
                        // Получен ответ на запрос.
                        {
                            // Передать ответ ожидающему потоку.
                            socketQueue.RequestQueue.OnResponse(message);
                        }
                    }
                    else
                    // Произошла ошибка при десериализации сообщения, необходимо аварийно закрыть соединение.
                    {
                        var protocolErrorException = new ProtocolErrorException(ProtocolErrorMessage, deserializationException);

                        // Сообщить потокам что обрыв произошел по вине удалённой стороны.
                        socketQueue.RequestQueue.OnDisconnect(protocolErrorException);

                        try
                        {
                            // Отключаемся от сокета с небольшим таймаутом.
                            using (var cts = new CancellationTokenSource(3000))
                                await socketQueue.WebSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Ошибка десериализации сообщения.", cts.Token);
                        }
                        catch (Exception ex)
                        // Злой обрыв соединения.
                        {
                            // Оповестить об обрыве.
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

                // Закрывает соединение.
                socketQueue.Dispose();

                // Отменить все операции контроллеров связанных с текущим соединением.
                _cts.Cancel();

                // Сообщить наследникам об обрыве.
                OnDisconnect(socketQueue);
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

            }, state: (socketQueue_, message_)); // Без замыкания.
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
        /// Не бросает исключения!
        /// </summary>
        private protected async Task SendMessageAsync(SocketQueue socketQueue, Message message)
        {
            // На текущем этапе сокет может быть уже уничтожен другим потоком
            // В результате чего в текущем потоке случилась ошибка но отправлять её не нужно.
            if (socketQueue.IsDisposed)
                return;

            using (var mem = new MemoryPoolStream(256))
            {
                mem.Position = Header.Size;

                int contentLength = 0;
                if (!message.IsRequest)
                {
                    ExtensionMethods.Serialize(message.Result, mem);
                    contentLength = (int)mem.Position - Header.Size;
                }

                var header = new Header
                {
                    IsRequest = message.IsRequest,
                    Uid = message.Uid,
                    ContentLength = contentLength,
                };

                mem.Position = 0;
                header.Serialize(mem);

                byte[] streamBuffer = mem.GetBuffer();

                try
                {
                    // Может бросить ObjectDisposedException.
                    using (await _sendMessageLock.LockAsync())
                    {
                        // Отправляем сообщение по частям.
                        int offset = 0;
                        int bytesLeft = (int)mem.Length;
                        do
                        {
                            bool endOfMessage = false;
                            int countToSend = WebSocketFrameMaxSize;
                            if (countToSend >= bytesLeft)
                            {
                                countToSend = bytesLeft;
                                endOfMessage = true;
                            }

                            var segment = new ArraySegment<byte>(streamBuffer, offset, countToSend);

                            try
                            {
                                await socketQueue.WebSocket.SendAsync(segment, WebSocketMessageType.Binary, endOfMessage, CancellationToken.None);
                            }
                            catch (Exception ex)
                            // Обрыв соединения.
                            {
                                // Оповестить об обрыве.
                                AtomicDisconnect(socketQueue, ex);

                                // Завершить поток.
                                return;
                            }

                            bytesLeft -= countToSend;
                            offset += countToSend;

                        } while (bytesLeft > 0);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Может случиться если вызвали Context.Dispose().
                }
            }
        }

        [DebuggerStepThrough]
        /// <exception cref="ObjectDisposedException"/>
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        public virtual void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                SocketQueue socket = Socket;
                if (socket != null)
                {
                    // Оповестить об обрыве.
                    AtomicDisconnect(socket, new ObjectDisposedException(GetType().FullName));
                }

                _sendMessageLock.Dispose();
            }
        }
    }
}
