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

namespace wRPC
{
    public sealed class Context : IDisposable
    {
        private readonly Type _controllerType;
        private readonly StandardKernel _ioc;
        private bool _disposed;
        public MyWebSocket WebSocket { get; }

        public Context(MyWebSocket webSocket, Type defaultController)
        {
            WebSocket = webSocket;
            _ioc = new StandardKernel();
            _ioc.Bind(defaultController).ToSelf();
            _controllerType = defaultController;
        }

        /// <summary>
        /// Вызывает запрошенный клиентом метод и возвращает результат.
        /// </summary>
        private async Task<object> InvokeActionAsync(Request request)
        {
            // Ядро IOC.
            // Резолвим контроллер.
            object controller = _ioc.Get(_controllerType);

            // Ищем делегат запрашиваемой функции.
            MethodInfo method = _controllerType.GetMethod(request.ActionName);

            object[] args = request.Args.Select(x => x.Value.ToObject()).ToArray();

            // Вызов делегата.
            object result = method.Invoke(controller, args);

            // Результатом делегата может быть Task.
            result = await DynamicAwaiter.ToAsync(result);

            return result;
        }

        /// <summary>
        /// Запускает цикл обработки запросов этого клиента.
        /// </summary>
        internal void StartReceive()
        {
            ReceaveAsync();
        }

        private async void ReceaveAsync()
        {
            // Бесконечно обрабатываем запросы пользователя.
            while (true)
            {
                Request request = null;

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
                        {
                            Debug.WriteLine(ex);

                            // Запрос клиента не удалось десериализовать.
                            await SendErrorResponseAsync(request, "Invalid Request Format Error");
                        }
                    }
                    #endregion
                }
                finally
                {
                    // Возвращаем память.
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                // Не блокируем обработку следующих запросов клиента.
                ThreadPool.UnsafeQueueUserWorkItem(req =>
                {
                    StartProcessRequestAsync((Request)req);
                }, state: request /* Избавляемся от замыкания */);
            }
        }

        /// <summary>
        /// Выполняет запрос клиента и отправляет ему результат или ошибку.
        /// </summary>
        private async void StartProcessRequestAsync(Request request)
        {
            Response response;
            
            try
            {
                // Выполнить запрашиваемую функцию
                object result = await InvokeActionAsync(request);

                // Запрашиваемая функция выполнена успешно.
                // Подготовить возвращаемый результат.
                response = OkResponse(request, result);
            }
            catch (Exception ex)
            // Ошибка обработки запроса.
            {
                Debug.WriteLine(ex);

                // Подготовить результат с ошибкой.
                response = ErrorResponse(request, "Internal Server Error");
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

        private async Task SendErrorResponseAsync(Request request, string errorMessage)
        {
            await SendResponseAsync(ErrorResponse(request, errorMessage));
        }

        private Response ErrorResponse(Request request, string errorMessage)
        {
            return new Response(request.Uid, MessagePackObject.Nil, errorMessage);
        }

        private Response OkResponse(Request request, object result)
        {
            return new Response(request.Uid, MessagePackObject.FromObject(result), null);
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

        public void Dispose()
        {
            if(!_disposed)
            {
                _disposed = true;
                _ioc.Dispose();
            }
        }
    }
}
