using Contract;
using DynamicMethodsLib;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Buffers;
using System.Collections.Generic;
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
        private readonly MyClientWebSocket _cli;
        private bool _disposed;

        static Client()
        {
            MessagePackSerializer.PrepareType<Request>();
            MessagePackSerializer.PrepareType<Response>();
        }

        // ctor.
        public Client()
        {
            _cli = new MyClientWebSocket();
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
            Response resp;
            using (var rentMem = MemoryPool<byte>.Shared.Rent(4096))
            {
                using (var stream = new SpanStream(rentMem.Memory))
                {
                    MessagePackSerializer.Get<Request>().Pack(stream, request);

                    await _cli.SendAsync(rentMem.Memory.Slice(0, (int)stream.Position), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);

                    ValueWebSocketReceiveResult message = await _cli.ReceiveAsync(rentMem.Memory, CancellationToken.None).ConfigureAwait(false);
                    
                    stream.Position = 0;
                    resp = MessagePackSerializer.Get<Response>().Unpack(stream);
                }
            }

            resp.EnsureSuccessStatusCode();

            object result = resp.Result.ToObject();
            return result;
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

        public Task ConnectAsync(string host, int port)
        {
            var wsAddress = new Uri($"ws://{host}:{port}");
            return _cli.ConnectAsync(wsAddress);
        }

        public Task ConnectAsync(EndPoint endPoint)
        {
            if (endPoint is IPEndPoint iPEndPoint)
            {
                return ConnectAsync(iPEndPoint.Address.ToString(), iPEndPoint.Port);
            }
            else
            {
                var ep = (DnsEndPoint)endPoint;
                return ConnectAsync(ep.Host, ep.Port);
            }
        }

        public void Dispose()
        {
            if(!_disposed)
            {
                _disposed = true;
                _cli.Dispose();
            }
        }
    }
}
