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

namespace wRPC
{
    public class Context
    {
        public MyWebSocket WebSocket { get; }
        private BaseController _controller { get; }

        public Context(MyWebSocket webSocket, BaseController controller)
        {
            WebSocket = webSocket;
            _controller = controller;
        }

        /// <summary>
        /// Вызывает запрошенный клиентом метод и возвращает результат.
        /// </summary>
        public object InvokeAction(Request request)
        {
            MethodInfo method = _controller.GetType().GetMethod(request.ActionName);
            object result = method.Invoke(_controller, request.Args.Select(x => x.ToObject()).ToArray());
            return result;
        }

        internal void StartReceive()
        {
            ReceaveAsync();
        }

        private async void ReceaveAsync()
        {
            while (true)
            {
                Request request = null;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
                try
                {
                    ValueWebSocketReceiveResult message;
                    try
                    {
                        message = await WebSocket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    if (message.EndOfMessage)
                    {
                        using (var mem = new MemoryStream(buffer, 0, message.Count))
                        {
                            //var ser = new JsonSerializer();
                            //using (var bson = new BsonDataReader(mem))
                            //{
                            //    request = ser.Deserialize<Request>(bson);
                            //}
                            //request = ProtoBuf.Serializer.Deserialize<Request>(mem);
                            request = MessagePackSerializer.Get<Request>().Unpack(mem);
                        }
                    }
                    else
                    {

                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                OnRequest(request);
            }
        }

        private void OnRequest(Request request)
        {
            object result;
            try
            {
                result = InvokeAction(request);
            }
            catch (Exception ex)
            {
                throw;
            }

            SendResponse(new Response(request.Uid, result));
        }

        private async void SendResponse(Response response)
        {
            byte[] buffer;
            using (var mem = new MemoryStream())
            {
                MessagePackSerializer.Get<Response>().Pack(mem, response);
                buffer = mem.ToArray();
            }

            try
            {
                await WebSocket.SendAsync(buffer.AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
