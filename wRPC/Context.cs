using Contract;
using DanilovSoft.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
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
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;
using MsgPack.Serialization;
using MsgPack;
using System.Diagnostics;
using Ninject;
using System.Collections.Concurrent;
using Ninject.Activation.Blocks;

namespace wRPC
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class Context
    {
        #region Debug
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{{{nameof(Context)}}}, UserId = {UserId}" + "}";
        #endregion

        private readonly StandardKernel _ioc;
        /// <summary>
        /// Сервер который принял текущее соединение.
        /// </summary>
        private readonly Listener _listener;
        /// <summary>
        /// Объект синхронизации текущего экземпляра.
        /// </summary>
        private readonly object _syncObj = new object();
        public MyWebSocket WebSocket { get; }
        public bool IsAuthorized { get; private set; }
        public int? UserId { get; private set; }
        /// <summary>
        /// Смежные соединения текущего пользователя.
        /// </summary>
        private UserConnections _connections;

        // ctor.
        internal Context(MyWebSocket webSocket, StandardKernel ioc, Listener listener)
        {
            WebSocket = webSocket;
            _ioc = ioc;
            _listener = listener;
        }

        public async Task SendMessageAsync(int fromUserId, string message)
        {
            var req = new Request
            {
                Uid = 0,
                ActionName = "IncMsg", // IncommingMessage
                Args = new[]
                {
                    new Request.Arg("message", MessagePackObject.FromObject(message))
                }
            };

            byte[] buffer = req.Serialize();

            // Потокобезопасная отправка.
            // TODO
            //throw new NotImplementedException();
            //await WebSocket.SendAsync(buffer, WebSocketMessageType.Binary, endOfMessage: true);
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
        private async Task<object> InvokeActionAsync(Request request)
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
            using (IActivationBlock iocBlock = _ioc.BeginBlock())
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
        private Type FindRequestedController(Request request, out string controllerName, out string actionName)
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
            _listener._controllers.TryGetValue(controllerName, out Type controllerType);

            return controllerType;
        }

        /// <summary>
        /// Производит маппинг аргументов запроса в соответствии с делегатом.
        /// </summary>
        private object[] GetParameters(MethodInfo method, Request request)
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
        /// Запускает цикл обработки запросов этого клиента.
        /// </summary>
        internal void StartReceive()
        {
            // Начать цикл обработки сообщений пользователя.
            ReceaveAsync();
        }

        private async void ReceaveAsync()
        {
            // Бесконечно обрабатываем запросы пользователя.
            while (true)
            {
                Request request = null;
                Response errorResponse = null;

                // Арендуем память.
                using (var buffer = new ArrayPool(4096))
                {
                    #region Читаем запрос из сокета

                    WebSocketReceiveResult message;
                    try
                    {
                        message = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer.Buffer), CancellationToken.None);
                    }
                    catch (Exception ex)
                    // Обрыв соединения.
                    {
                        Debug.WriteLine(ex);
                        return;
                    }
                    #endregion

                    #region Десериализуем запрос

                    using (var mem = new MemoryStream(buffer.Buffer, 0, message.Count))
                    {
                        try
                        {
                            request = MessagePackSerializer.Get<Request>().Unpack(mem);
                        }
                        catch (Exception ex)
                        // Ошибка десериализации запроса.
                        {
                            Debug.WriteLine(ex);

                            // Подготоваить ответ с ошибкой.
                            errorResponse = request.ErrorResponse("Invalid Request Format Error", ErrorCode.InvalidRequestFormat);
                        }
                    }
                    #endregion
                }

                if (errorResponse == null)
                // Запрос десериализован без ошибок.
                {
                    // Начать выполнение запроса не блокируя обработку следующих запросов этого клиента.
                    ThreadPool.UnsafeQueueUserWorkItem(StartProcessRequestAsync, request);
                }
                else
                // Произошла ошибка при разборе запроса.
                {
                    // Отправить результат с ошибкой не блокируя обработку следующих запросов этого клиента.
                    ThreadPool.UnsafeQueueUserWorkItem(SendErrorResponse, errorResponse);
                }
            }
        }

        private async void SendErrorResponse(object state)
        {
            var errorResponse = (Response)state;

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
            var request = (Request)state;

            // Выполнить запрос и получить инкапсулированный результат.
            Response response = await GetResponseAsync(request);

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
        private async Task<Response> GetResponseAsync(Request request)
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
                Debug.WriteLine(ex);

                // Подготовить результат с ошибкой.
                return request.ErrorResponse("Internal Server Error", ErrorCode.InternalError);
            }
        }

        private Response OkResponse(Request request, object result)
        {
            return new Response(request.Uid, MessagePackObject.FromObject(result), error: null, errorCode: null);
        }

        private async Task SendResponseAsync(Response response)
        {
            byte[] buffer = response.Serialize();
            await WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
        }
    }
}
