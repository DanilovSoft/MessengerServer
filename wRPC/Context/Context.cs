using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using DynamicMethodsLib;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using DanilovSoft.WebSocket;
using System.Net.Sockets;

namespace wRPC
{
    /// <summary>
    /// Контекст соединения Web-Сокета. Владеет соединением.
    /// </summary>
    public abstract class Context : IDisposable
    {
        /// <summary>
        /// Максимальный размер фрейма который может передавать протокол.
        /// </summary>
        private const int WebSocketMaxFrameSize = 4096;
        private const string ProtocolHeaderErrorMessage = "Произошла ошибка десериализации заголовка от удалённой стороны.";
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
        private readonly Dictionary<string, Type> _controllers;
        /// <summary>
        /// Содержит все доступные для вызова экшены.
        /// </summary>
        private readonly ControllerActionsDictionary _controllerActions;
        private ServiceProvider _serviceProvider;
        private protected ServiceProvider ServiceProvider => _serviceProvider;
        private protected volatile SocketQueue _socket;
        /// <summary>
        /// Является <see langword="volatile"/>.
        /// </summary>
        private protected SocketQueue Socket { get => _socket; set => _socket = value; }
        /// <summary>
        /// Токен отмены связанный с текущим соединением. Позволяет прервать действия контроллера при обрыве соединения.
        /// </summary>
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        /// <summary>
        /// Токен отмены связанный с текущим соединением.
        /// </summary>
        public CancellationToken CancellationToken => _cts.Token;
        /// <summary>
        /// Отправка сообщения <see cref="Message"/> должна выполняться только с захватом этой блокировки.
        /// </summary>
        //private readonly AsyncLock _sendMessageLock = new AsyncLock();
        private readonly Channel<SendJob> _sendChannel;
        private int _disposed;
        private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

        // static ctor.
        static Context()
        {
            ProtoBuf.Serializer.PrepareSerializer<Header>();
        }

        // ctor.
        /// <summary>
        /// Конструктор клиента.
        /// </summary>
        /// <param name="controllersAssembly">Сборка в которой будет осеществляться поиск контроллеров.</param>
        internal Context(Assembly controllersAssembly) : this()
        {
            // Сборка с контроллерами не должна быть текущей сборкой.
            Debug.Assert(controllersAssembly != Assembly.GetExecutingAssembly());

            // Словарь с найденными контроллерами в вызывающей сборке.
            _controllers = GlobalVars.FindAllControllers(controllersAssembly);

            _controllerActions = new ControllerActionsDictionary(_controllers);

            // Запустить диспетчер отправки сообщений.
            _sendChannel = StartChannelSender();
        }

        // ctor.
        /// <summary>
        /// Конструктор сервера.
        /// </summary>
        /// <param name="ioc">Контейнер Listener'а.</param>
        internal Context(MyWebSocket clientConnection, ServiceProvider serviceProvider, Dictionary<string, Type> controllers) : this()
        {
            // У сервера сокет всегда подключен и переподключаться не может.
            Socket = new SocketQueue(clientConnection);

            // IoC готов к работе.
            _serviceProvider = serviceProvider;

            // Копируем список контроллеров сервера.
            _controllers = controllers;

            _controllerActions = new ControllerActionsDictionary(controllers);

            // Запустить диспетчер отправки сообщений.
            _sendChannel = StartChannelSender();
        }

        // ctor.
        private Context()
        {
            
        }

        /// <summary>
        /// Вызывается единожды клиентским контектом.
        /// </summary>
        private protected void ConfigureIoC(ServiceCollection ioc)
        {
            // Добавим в IoC все контроллеры сборки.
            foreach (Type controllerType in _controllers.Values)
                ioc.AddScoped(controllerType);

            var serviceProvider = ioc.BuildServiceProvider();

            // IoC готов к работе.
            if(Interlocked.CompareExchange(ref _serviceProvider, serviceProvider, null) != null)
            {
                // Нельзя устанавливать IoC повторно.
                serviceProvider.Dispose();
            }
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
            Arg[] CreateArgs()
            {
                ParameterInfo[] par = targetMethod.GetParameters();
                Arg[] retArgs = new Arg[par.Length];

                for (int i = 0; i < par.Length; i++)
                {
                    ParameterInfo p = par[i];
                    retArgs[i] = new Arg(p.Name, args[i]);
                }
                return retArgs;
            }
            #endregion

            // Подготавливаем запрос для отправки.
            var requestToSend = Message.CreateRequest($"{controllerName}/{targetMethod.Name}", CreateArgs());

            // Тип результата инкапсулированный в Task<T>.
            Type resultType = GetActionReturnType(targetMethod);

            // Отправляет запрос и получает результат от удалённой стороны.
            Task<object> taskObject = ExecuteRequestAsync(requestToSend, resultType, Socket);

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
                    // Результатом может быть исключение.
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

        /// <summary>
        /// Происходит атомарно для экземпляра подключения.
        /// </summary>
        /// <param name="socketQueue">Соединение в котором произошло отключение.</param>
        private protected abstract void OnAtomicDisconnect(SocketQueue socketQueue);

        /// <summary>
        /// Отправляет запрос о ожидает его ответ.
        /// </summary>
        /// <param name="returnType">Тип в который будет десериализован результат запроса.</param>
        private protected async Task<object> ExecuteRequestAsync(Message requestToSend, Type returnType, SocketQueue socketQueue)
        {
            // У клиента соединение может быть ещё не установлено.
            if (socketQueue == null)
            {
                // Никогда не вызывается серверным контекстом.
                socketQueue = await GetOrCreateConnectionAsync().ConfigureAwait(false);
            }

            // Добавить запрос в словарь для дальнейшей связки с ответом.
            TaskCompletionSource tcs = socketQueue.RequestCollection.AddRequest(returnType, requestToSend.ActionName, out short uid);

            // Назначить запросу уникальный идентификатор.
            requestToSend.Uid = uid;

            // Планируем отправку запроса.
            QueueSendMessage(socketQueue, requestToSend);

            // Ожидаем результат от потока поторый читает из сокета.
            object rawResult = await tcs;

            // Успешно получили результат без исключений.
            return rawResult;
        }

        /// <summary>
        /// Запускает бесконечный цикл, в фоновом потоке, считывающий из сокета запросы и ответы.
        /// </summary>
        private protected void StartReceivingLoop(SocketQueue socketQueue_)
        {
            ThreadPool.UnsafeQueueUserWorkItem(async state =>
            {
                var socketQueue = (SocketQueue)state;

                // Бесконечно обрабатываем сообщения сокета.
                do
                {
                    int headerLength = 0;

                    // Арендуем память на фрейм веб-сокета.
                    using (var mem = new MemoryPoolStream(128))
                    {
                        #region Читаем все фреймы веб-сокета в стрим.

                        Header header = null;
                        ValueWebSocketReceiveExResult webSocketMessage;

                        // Арендуем максимум памяти на один фрейм веб-сокета.
                        using (var frameMem = new MemoryPoolStream(WebSocketMaxFrameSize))
                        {
                            byte[] pooledBuffer = frameMem.DangerousGetBuffer();
                            var segment = new ArraySegment<byte>(pooledBuffer);
                            do
                            {
                                #region Читаем фрейм веб-сокета.

                                try
                                {
                                    // Читаем фрейм веб-сокета.
                                    webSocketMessage = await socketQueue.WebSocket.ReceiveExAsync(segment, CancellationToken.None);
                                }
                                catch (Exception ex)
                                // Обрыв соединения.
                                {
                                    // Оповестить об обрыве.
                                    AtomicDisconnect(socketQueue, ex);

                                    // Завершить поток.
                                    return;
                                }

                                if (webSocketMessage.ErrorCode == SocketError.Success)
                                {
                                    #endregion

                                    #region Проверка на Close.

                                    // Другая сторона закрыла соединение.
                                    if (webSocketMessage.MessageType == WebSocketMessageType.Close)
                                    {
                                        // Сформировать причину закрытия соединения.
                                        string exceptionMessage = GetMessageFromCloseFrame(webSocketMessage, socketQueue.WebSocket);

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

                                    if (header == null && Header.TryReadLengthPrefix(mem, out int length) && mem.Length >= (length + 4))
                                    // Пора десериализовать заголовок.
                                    {
                                        #region Десериализуем фрейм веб-сокета в заголовок протокола.

                                        mem.Position = 0;
                                        try
                                        {
                                            header = Header.DeserializeWithLengthPrefix(mem);
                                        }
                                        catch (Exception headerException)
                                        // Не удалось десериализовать заголовок.
                                        {
                                            var protocolErrorException = new ProtocolErrorException(ProtocolHeaderErrorMessage, headerException);

                                            // Сообщить потокам что обрыв произошел по вине удалённой стороны.
                                            socketQueue.RequestCollection.OnDisconnect(protocolErrorException);

                                            try
                                            {
                                                // Отключаемся от сокета с небольшим таймаутом.
                                                using (var cts = new CancellationTokenSource(3000))
                                                    await socketQueue.WebSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Ошибка десериализации заголовка.", cts.Token);
                                            }
                                            catch (Exception ex)
                                            // Злой обрыв соединения.
                                            {
                                                // Оповестить об обрыве.
                                                AtomicDisconnect(socketQueue, ex);

                                                // Завершить поток.
                                                return;
                                            }

                                            // Оповестить об обрыве.
                                            AtomicDisconnect(socketQueue, protocolErrorException);

                                            // Завершить поток.
                                            return;
                                        }

                                        // Размер заголовка определён.
                                        headerLength = (int)mem.Position;

                                        // Если есть еще фреймы веб-сокета.
                                        if (!webSocketMessage.EndOfMessage)
                                        {
                                            //mem.Position = mem.Length;

                                            // Возвращаем позицию стрима в конец для следующей записи.
                                            mem.Seek(0, SeekOrigin.End);

                                            // Увеличим стрим до размера всего сообщения.
                                            mem.Capacity = (header.ContentLength + headerLength);
                                        }

                                        #endregion
                                    }
                                }
                                else
                                // Обрыв соединения.
                                {
                                    // Оповестить об обрыве.
                                    AtomicDisconnect(socketQueue, new SocketException((int)webSocketMessage.ErrorCode));

                                    // Завершить поток.
                                    return;
                                }
                            } while (!webSocketMessage.EndOfMessage);
                        }
                        #endregion

                        if (header != null)
                        {
                            // Установить курсор после заголовка.
                            mem.Position = headerLength;

                            if (header.StatusCode == StatusCode.Request)
                            // Получен запрос.
                            {
                                RequestMessage receivedRequest;
                                try
                                {
                                    receivedRequest = ExtensionMethods.DeserializeJson<RequestMessage>(mem);
                                }
                                catch (Exception ex)
                                // Ошибка десериализации запроса.
                                {
                                    // Подготовить ответ с ошибкой.
                                    var errorResponse = Message.FromResult(header.Uid, new InvalidRequestResult($"Не удалось десериализовать запрос. Ошибка: \"{ex.Message}\"."));

                                    // Передать на отправку результата с ошибкой.
                                    QueueSendMessage(socketQueue, errorResponse);

                                    // Вернуться к чтению из сокета.
                                    continue;
                                }
                                // Запрос успешно десериализован.
                                receivedRequest.Header = header;

                                // Установить контекст запроса.
                                receivedRequest.RequestContext = new RequestContext();

                                // Начать выполнение запроса в отдельном потоке.
                                StartProcessRequestAsync(socketQueue, receivedRequest);
                            }
                            else
                            // Получен ответ на запрос.
                            {
                                // Удалить запрос из словаря.
                                if (socketQueue.RequestCollection.TryTake(header.Uid, out TaskCompletionSource tcs))
                                // Передать ответ ожидающему потоку.
                                {
                                    if (header.StatusCode == StatusCode.Ok)
                                    // Запрос на удалённой стороне был выполнен успешно.
                                    {
                                        if (tcs.ResultType != typeof(void))
                                        {
                                            // Десериализатор в соответствии с ContentEncoding.
                                            var deserializer = header.GetDeserializer();

                                            object rawResult;
                                            try
                                            {
                                                rawResult = deserializer(mem, tcs.ResultType);
                                            }
                                            catch (Exception deserializationException)
                                            {
                                                var protocolErrorException = new ProtocolErrorException($"Произошла ошибка десериализации " +
                                                    $"результата запроса типа \"{tcs.ResultType.FullName}\"", deserializationException);

                                                // Сообщить ожидающему потоку что произошла ошибка при разборе ответа удаленной стороны.
                                                tcs.TrySetException(protocolErrorException);

                                                // Вернуться к чтению из сокета.
                                                continue;
                                            }
                                            // Передать результат ожидающему потоку.
                                            tcs.TrySetResult(rawResult);
                                        }
                                        else
                                        // void.
                                        {
                                            tcs.TrySetResult(null);
                                        }
                                    }
                                    else
                                    // Сервер прислал код ошибки.
                                    {
                                        // Телом ответа в этом случае будет строка.
                                        string errorMessage = mem.ReadAsString();
                                        
                                        // Сообщить ожидающему потоку что удаленная сторона вернула ошибку в результате выполнения запроса.
                                        tcs.TrySetException(new RemoteException(errorMessage, header.StatusCode));
                                    }
                                }
                            }
                        }
                        else
                        // Хедер не получен.
                        {
                            var protocolErrorException = new ProtocolErrorException("Удалённая сторона прислала недостаточно данных для заголовка.");

                            // Сообщить потокам что обрыв произошел по вине удалённой стороны.
                            socketQueue.RequestCollection.OnDisconnect(protocolErrorException);

                            try
                            {
                                // Отключаемся от сокета с небольшим таймаутом.
                                using (var cts = new CancellationTokenSource(3000))
                                    await socketQueue.WebSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Непредвиденное завершение потока данных.", cts.Token);
                            }
                            catch (Exception ex)
                            // Злой обрыв соединения.
                            {
                                // Оповестить об обрыве.
                                AtomicDisconnect(socketQueue, ex);

                                // Завершить поток.
                                return;
                            }

                            // Оповестить об обрыве.
                            AtomicDisconnect(socketQueue, protocolErrorException);

                            // Завершить поток.
                            return;
                        }
                    }
                } while (true);
            }, state: socketQueue_); // Без замыкания.
        }

        /// <summary>
        /// Сериализует сообщение и передает на отправку другому потоку. Не бросает исключения!
        /// </summary>
        private void QueueSendMessage(SocketQueue socketQueue, Message messageToSend)
        {
            // На текущем этапе сокет может быть уже уничтожен другим потоком
            // В результате чего в текущем потоке случилась ошибка но отправлять её не нужно.
            if (socketQueue.IsDisposed)
                return;

            MemoryPoolStream mem;

            // Сериализуем контент.
            using (var contentStream = new MemoryPoolStream())
            {
                ActionContext actionContext = null;

                // Записать в стрим запрос или результат запроса.
                if (messageToSend.IsRequest)
                {
                    var request = new RequestMessage
                    {
                        ActionName = messageToSend.ActionName,
                        Args = messageToSend.Args,
                    };
                    ExtensionMethods.SerializeObjectJson(contentStream, request);
                }
                else
                // Ответ на запрос.
                {
                    actionContext = new ActionContext(this, contentStream, messageToSend.ReceivedRequest?.RequestContext);

                    // Записать контент.
                    Execute(messageToSend.Result, actionContext);
                }

                // Размер контента.
                int contentLength = (int)contentStream.Length;

                // Готовим заголовок.
                var header = new Header(messageToSend.Uid, actionContext?.StatusCode ?? StatusCode.Request)
                {
                    ContentLength = contentLength,
                };

                if (actionContext != null)
                {
                    // Записать в заголовок формат контента.
                    header.ContentEncoding = actionContext.ProducesEncoding;
                }

                mem = new MemoryPoolStream(contentLength);

                // Записать заголовок в самое начало с размер-префиксом.
                header.SerializeWithLengthPrefix(mem);

                byte[] buffer = contentStream.DangerousGetBuffer();
                mem.Write(buffer, 0, contentLength);
            }

            // Из-за AllowSynchronousContinuations частично начнёт отправку текущим потоком(!).
            if (!_sendChannel.Writer.TryWrite(new SendJob(socketQueue, mem)))
            {
                mem.Dispose();
            }
        }

        private void Execute(object rawResult, ActionContext actionContext)
        {
            if (rawResult is IActionResult actionResult)
            {
                actionResult.ExecuteResult(actionContext);
            }
            else
            {
                actionContext.StatusCode = StatusCode.Ok;
                actionContext.Request.ActionToInvoke.SerializeObject(actionContext.ResponseStream, rawResult);
                actionContext.ProducesEncoding = actionContext.Request.ActionToInvoke.ProducesEncoding;
            }
        }

        /// <summary>
        /// Принимает заказы на отправку и отправляет в сокет.
        /// </summary>
        private Channel<SendJob> StartChannelSender()
        {
            var sendChannel = Channel.CreateUnbounded<SendJob>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true, // Внимательнее с этим параметром!
                SingleReader = true,
                SingleWriter = false,
            });

            // Делегат не должен бросать исключения!
            ThreadPool.UnsafeQueueUserWorkItem(async state =>
            {
                var channel = (Channel<SendJob>)state;

                do
                {
                    SendJob sendJob;
                    try
                    {
                        sendJob = await channel.Reader.ReadAsync();
                    }
                    catch (ChannelClosedException)
                    {
                        // Завершить поток.
                        return;
                    }

                    // Этим стримом теперь владеет только этот поток.
                    using (MemoryPoolStream mem = sendJob.MemoryPoolStream)
                    {
                        // Отправляем сообщение по частям.
                        int offset = 0;
                        int bytesLeft = (int)mem.Length;
                        do
                        {
                            bool endOfMessage = false;
                            int countToSend = WebSocketMaxFrameSize;
                            if (countToSend >= bytesLeft)
                            {
                                countToSend = bytesLeft;
                                endOfMessage = true;
                            }

                            byte[] streamBuffer = mem.DangerousGetBuffer();

                            var segment = new ArraySegment<byte>(streamBuffer, offset, countToSend);

                            try
                            {
                                await sendJob.SocketQueue.WebSocket.SendAsync(segment, WebSocketMessageType.Binary, endOfMessage, CancellationToken.None);
                            }
                            catch (Exception ex)
                            // Обрыв соединения.
                            {
                                // Оповестить об обрыве.
                                AtomicDisconnect(sendJob.SocketQueue, ex);

                                // Завершить поток.
                                return;
                            }

                            bytesLeft -= countToSend;
                            offset += countToSend;

                        } while (bytesLeft > 0);
                    }
                } while (true);
            }, state: sendChannel);

            return sendChannel;
        }

        /// <summary>
        /// Потокобезопасно освобождает ресурсы соединения. Вызывается при обрыве соединения.
        /// </summary>
        /// <param name="socketQueue">Экземпляр в котором произошел обрыв.</param>
        /// <param name="exception">Возможная причина обрыва соединения.</param>
        private protected void AtomicDisconnect(SocketQueue socketQueue, Exception exception)
        {
            // Захватить эксклюзивный доступ к сокету.
            if(socketQueue.TryOwn())
            {
                // Передать исключение всем ожидающим потокам.
                socketQueue.RequestCollection.OnDisconnect(exception);

                // Закрыть соединение.
                socketQueue.Dispose();

                // Отменить все операции контроллеров связанных с текущим соединением.
                _cts.Cancel();

                // Сообщить наследникам об обрыве.
                OnAtomicDisconnect(socketQueue);
            }
        }

        /// <summary>
        /// Формирует сообщение ошибки из фрейма веб-сокета информирующем о закрытии соединения.
        /// </summary>
        private string GetMessageFromCloseFrame(ValueWebSocketReceiveExResult webSocketMessage, DanilovSoft.WebSocket.WebSocket webSocket)
        {
            string exceptionMessage = null;
            if (webSocket.CloseStatus != null)
            {
                exceptionMessage = $"CloseStatus: {webSocket.CloseStatus.ToString()}";

                if (!string.IsNullOrEmpty(webSocket.CloseStatusDescription))
                {
                    exceptionMessage += $", Description: \"{webSocket.CloseStatusDescription}\"";
                }
            }
            else if (!string.IsNullOrEmpty(webSocket.CloseStatusDescription))
            {
                exceptionMessage = $"Description: \"{webSocket.CloseStatusDescription}\"";
            }

            if (exceptionMessage == null)
                exceptionMessage = "Удалённая сторона закрыла соединение без объяснения причины.";

            return exceptionMessage;
        }

        /// <summary>
        /// Вызывает запрошенный метод контроллера и возвращает результат.
        /// </summary>
        /// <exception cref="RemoteException"/>
        private async Task<object> InvokeControllerAsync(RequestMessage receivedRequest)
        {
            // Находим контроллер.
            Type controllerType = FindRequestedController(receivedRequest, out string controllerName, out string actionName);
            if(controllerType == null)
                throw new RemoteException($"Unable to find requested controller \"{controllerName}\"", StatusCode.ActionNotFound);

            // Ищем делегат запрашиваемой функции.
            if (!_controllerActions.TryGetValue(controllerType, actionName, out ControllerAction action))
                throw new RemoteException($"Unable to find requested action \"{receivedRequest.ActionName}\"", StatusCode.ActionNotFound);

            // Контекст запроса запоминает запрашиваемый метод.
            receivedRequest.RequestContext.ActionToInvoke = action;

            // Проверить доступ к функции.
            InvokeMethodPermissionCheck(action.TargetMethod, controllerType);

            // Блок IoC выполнит Dispose всем созданным экземплярам.
            using (IServiceScope scope = ServiceProvider.CreateScope())
            {
                // Активируем контроллер через IoC.
                using (var controller = (Controller)scope.ServiceProvider.GetRequiredService(controllerType))
                {
                    // Подготавливаем контроллер.
                    BeforeInvokePrepareController(controller);

                    // Мапим аргументы по их именам.
                    object[] args = GetParameters(action.TargetMethod, receivedRequest);

                    // Вызов делегата.
                    object controllerResult = action.TargetMethod.InvokeFast(controller, args);

                    if (controllerResult != null)
                    {
                        // Извлекает результат из Task'а.
                        controllerResult = await DynamicAwaiter.WaitAsync(controllerResult);
                    }

                    // Результат успешно получен без исключений.
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
        private Type FindRequestedController(RequestMessage request, out string controllerName, out string actionName)
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

            //controllerName += "Controller";

            // Ищем контроллер в кэше.
            _controllers.TryGetValue(controllerName, out Type controllerType);

            return controllerType;
        }

        /// <summary>
        /// Производит маппинг аргументов запроса в соответствии с делегатом.
        /// </summary>
        private object[] GetParameters(MethodInfo method, RequestMessage request)
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
        /// В новом потоке выполняет запрос клиента и отправляет ему результат или ошибку.
        /// </summary>
        private void StartProcessRequestAsync(SocketQueue socketQueue_, RequestMessage receivedRequest_)
        {
            ThreadPool.UnsafeQueueUserWorkItem(async state =>
            {
                var tuple = ((SocketQueue socketQueue, RequestMessage receivedRequest))state;

                // Выполнить запрос и создать сообщение с результатом.
                Message responseToSend = await GetResponseAsync(tuple.receivedRequest);

                // Сериализовать и отправить результат.
                QueueSendMessage(tuple.socketQueue, responseToSend);

            }, state: (socketQueue_, receivedRequest_)); // Без замыкания.
        }

        /// <summary>
        /// Выполняет запрос клиента и инкапсулирует результат в <see cref="Response"/>. Не бросает исключения.
        /// </summary>
        private async Task<Message> GetResponseAsync(RequestMessage receivedRequest)
        {
            object rawResult;
            Message messageToSend;

            try
            {
                // Выполнить запрашиваемую функцию.
                rawResult = await InvokeControllerAsync(receivedRequest);
            }
            catch (Exception ex)
            // Злая ошибка обработки запроса. Аналогично ошибке 500.
            {
                // Прервать отладку.
                //DebugOnly.Break();

                Debug.WriteLine(ex);

                // Вернуть результат с ошибкой.
                return Message.FromResult(receivedRequest, new InternalErrorResult("Internal Server Error"));
            }

            // Запрашиваемая функция выполнена успешно.
            // Подготовить возвращаемый результат.
            return Message.FromResult(receivedRequest, rawResult);
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
                var disposedException = new ObjectDisposedException(GetType().FullName);

                SocketQueue socket = Socket;
                if (socket != null)
                {
                    // Оповестить об обрыве.
                    AtomicDisconnect(socket, disposedException);
                }

                _serviceProvider?.Dispose();
                _sendChannel.Writer.TryComplete(error: disposedException);
            }
        }
    }
}
