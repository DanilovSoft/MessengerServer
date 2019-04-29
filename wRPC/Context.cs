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

namespace wRPC
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class Context
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{{{nameof(Context)}}}, UserId = {UserId}" + "}";

        private readonly StandardKernel _ioc;
        /// <summary>
        /// Сервер который принял текущее соединение.
        /// </summary>
        private readonly Listener _listener;
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
            //await WebSocket.SendAsync(buffer, WebSocketMessageType.Binary, endOfMessage: true);
        }

        public void Authorize(int userId)
        {
            // Авторизуем контекст пользователя.
            UserId = userId;
            IsAuthorized = true;

            // Добавляем соединение в словарь.
            _connections = AddConnection(userId);

            // Подпишемся на дисконнект.
            // TODO
            // Событие сработает даже если соединение уже разорвано.
            WebSocket.Disconnected += WebSocket_Disconnected;
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

            // Активируем контроллер через IoC.
            using (var controller = (BaseController)_ioc.Get(controllerType))
            {
                // Подготавливаем контроллер.
                controller.Context = this;
                controller.Listener = _listener;

                // Мапим аргументы по их именам.
                object[] args = GetParameters(method, request);

                // Вызов делегата.
                object result = method.Invoke(controller, args);

                // Результатом делегата может быть Task.
                result = await DynamicAwaiter.ToAsync(result);

                // Результат успешно получен.
                return result;
            }
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
                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
                try
                {
                    #region Читаем запрос из сокета

                    ValueWebSocketReceiveResult message;
                    try
                    {
                        message = await WebSocket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);
                    }
                    catch (Exception ex)
                    // Обрыв соединения.
                    {
                        Debug.WriteLine(ex);
                        return;
                    }
                    #endregion

                    #region Десериализуем запрос

                    using (var mem = new MemoryStream(buffer, 0, message.Count))
                    {
                        try
                        {
                            request = MessagePackSerializer.Get<Request>().Unpack(mem);
                        }
                        catch (Exception ex)
                        // Ошибка десериализации запроса.
                        {
                            Debug.WriteLine(ex);
                            errorResponse = request.ErrorResponse("Invalid Request Format Error", ErrorCode.InvalidRequestFormat);
                        }
                    }
                    #endregion
                }
                finally
                {
                    // Возвращаем память.
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if(errorResponse == null)
                {
                    // Не блокируем обработку следующих запросов этого клиента.
                    ThreadPool.UnsafeQueueUserWorkItem(StartProcessRequestAsync, request);
                }
                else
                // Произошла ошибка при разборе запроса.
                {
                    SendErrorResponse(errorResponse);
                }
            }
        }

        private void SendErrorResponse(Response errorResponse)
        {
            // Не блокируем обработку следующих запросов этого клиента.
            ThreadPool.UnsafeQueueUserWorkItem(async r =>
            {
                try
                {
                    await SendResponseAsync((Response)r);
                }
                catch (Exception ex)
                // Обрыв соединения.
                {
                    Debug.WriteLine(ex);
                }
            }, errorResponse);
        }

        /// <summary>
        /// Выполняет запрос клиента и отправляет ему результат или ошибку.
        /// </summary>
        private async void StartProcessRequestAsync(object state)
        {
            var request = (Request)state;
            Response response;
            
            try
            {
                // Выполнить запрашиваемую функцию
                object result = await InvokeActionAsync(request);

                // Запрашиваемая функция выполнена успешно.
                // Подготовить возвращаемый результат.
                response = OkResponse(request, result);
            }
            catch (RemoteException ex)
            // Дружелюбная ошибка.
            {
                // Подготовить результат с ошибкой.
                response = request.ErrorResponse(ex);
            }
            catch (Exception ex)
            // Злая ошибка обработки запроса.
            {
                Debug.WriteLine(ex);

                // Подготовить результат с ошибкой.
                response = request.ErrorResponse("Internal Server Error", ErrorCode.InternalError);
            }

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

        private Response OkResponse(Request request, object result)
        {
            return new Response(request.Uid, MessagePackObject.FromObject(result), error: null, errorCode: null);
        }

        private async Task SendResponseAsync(Response response)
        {
            byte[] buffer;
            using (var mem = new MemoryStream())
            {
                MessagePackSerializer.Get<Response>().Pack(mem, response);
                buffer = mem.ToArray();
            }

            await WebSocket.SendAsync(buffer.AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}
