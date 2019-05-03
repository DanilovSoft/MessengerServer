using Contract;
using DynamicMethodsLib;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace wRPC
{
    public sealed class Client : IDisposable
    {
        private readonly MyClientWebSocket _ws;
        private readonly RequestQueue _requestQueue;
        private readonly AsyncLock _asyncLock;
        private readonly Uri _uri;
        private volatile bool _connected;
        private bool _disposed;

        static Client()
        {
            MessagePackSerializer.PrepareType<Request>();
            MessagePackSerializer.PrepareType<Response>();
        }

        // ctor.
        public Client(string host, int port) : this(new Uri($"ws://{host}:{port}")) { }

        // ctor.
        public Client(Uri uri)
        {
            _uri = uri;
            _ws = new MyClientWebSocket();
            _requestQueue = new RequestQueue();
            _asyncLock = new AsyncLock();
        }

        public T GetProxy<T>(string controllerName)
        {
            T proxy = TypeProxy.Create<T, InterfaceProxy>((this, controllerName));
            return proxy;
        }

        /// <summary>
        /// Происходит при обращении к проксирующему интерфейсу.
        /// </summary>
        internal object OnProxyCall(MethodInfo targetMethod, object[] args, string controllerName)
        {
            var request = new Request
            {
                ActionName = $"{controllerName}/{targetMethod.Name}",
                Args = CreateArgs(targetMethod, args)
            };

            request.Uid = new object().GetHashCode();

            // Задача с ответом сервера.
            Task<object> taskObject = GetResponseAsync(request);

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

        private async Task<object> GetResponseAsync(Request request)
        {
            // Добавить в очередь запросов.
            TaskCompletionSource tcs = _requestQueue.CreateRequest();
            request.Uid = tcs.Uid;

            // Арендуем память.
            using (var rentMem = MemoryPool<byte>.Shared.Rent(4096))
            {
                // Замена MemoryStream.
                using (var stream = new SpanStream(rentMem.Memory))
                {
                    // Сериализуем запрос в память.
                    MessagePackSerializer.Get<Request>().Pack(stream, request);

                    // Выполнить подключение сокета если еще не подключен.
                    await ConnectIfNeededAsync().ConfigureAwait(false);

                    // Отправка запроса.
                    await _ws.SendAsync(rentMem.Memory.Slice(0, (int)stream.Position), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                }
            }

            // Ожидаем результат от потока поторый читает из сокета.
            Response response = await tcs;

            response.EnsureSuccessStatusCode();

            object rawResult = response.Result.ToObject();
            return rawResult;
        }

        /// <summary>
        /// Выполнить подключение сокета если еще не подключен.
        /// </summary>
        /// <returns></returns>
        private async ValueTask ConnectIfNeededAsync()
        {
            // Fast-path.
            if(_connected)
            {
                return;
            }
            else
            {
                using (await _asyncLock.LockAsync().ConfigureAwait(false))
                {
                    if(!_connected)
                    {
                        // Копия ссылки.
                        MyClientWebSocket ws = _ws;

                        await ws.ConnectAsync(_uri).ConfigureAwait(false);
                        _connected = true;
                        ThreadPool.UnsafeQueueUserWorkItem(ReceivingLoop, ws);
                    }
                }
            }
        }

        private async void ReceivingLoop(object state)
        {
            var ws = (MyClientWebSocket)state;
            using (var rentMem = MemoryPool<byte>.Shared.Rent(4096))
            {
                using (var stream = new SpanStream(rentMem.Memory))
                {
                    while (ws.State == WebSocketState.Open)
                    {
                        ValueWebSocketReceiveResult message;
                        try
                        {
                            message = await ws.ReceiveAsync(rentMem.Memory, CancellationToken.None);
                        }
                        catch (Exception ex)
                        // Разрыв соединения.
                        {
                            Debug.WriteLine(ex);

                            // Завершить текущий поток.
                            return;
                        }

                        stream.Position = 0;

                        #region Десериализация.

                        Response response;
                        try
                        {
                            response = MessagePackSerializer.Get<Response>().Unpack(stream);
                        }
                        catch (Exception ex)
                        // Не должно быть ошибок десериализации.
                        {
                            Debug.WriteLine(ex);
                            try
                            {
                                await ws.CloseAsync(WebSocketCloseStatus.ProtocolError, "Invalid Request Format Error", CancellationToken.None);
                            }
                            catch { }
                            return;
                        }
                        #endregion

                        // Передать ответ в очередь запросов.
                        _requestQueue.OnResponse(response);
                    }
                }
            }
        }

        private Request.Arg[] CreateArgs(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] par = targetMethod.GetParameters();
            Request.Arg[] retArgs = new Request.Arg[par.Length];

            for (int i = 0; i < par.Length; i++)
            {
                ParameterInfo p = par[i];
                retArgs[i] = new Request.Arg(p.Name, MessagePackObject.FromObject(args[i]));
            }
            return retArgs;
        }

        public ValueTask ConnectAsync()
        {
            return ConnectIfNeededAsync();
        }

        public void Dispose()
        {
            if(!_disposed)
            {
                _disposed = true;
                _ws.Dispose();
                _asyncLock.Dispose();
            }
        }
    }
}
