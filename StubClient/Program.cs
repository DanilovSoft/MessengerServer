using Contract;
using MsgPack;
using MsgPack.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace StubClient
{
    class Program
    {
        static async Task Main()
        {
            using (var cli = new DanilovSoft.WebSocket.ClientWebSocket())
            {
                var request = new Request
                {
                    ActionName = "Authorize",
                    Args = new[]
                    {
                        new Request.Arg("Login", "login"),
                        new Request.Arg("Password", "password")
                    }
                };

                request.Uid = new object().GetHashCode();

                byte[] buffer;
                using (var mem = new MemoryStream())
                {
                    var ser = MessagePackSerializer.Get<Request>();
                    ser.Pack(mem, request);
                    buffer = mem.ToArray();
                }

                Thread.Sleep(1000);
                await cli.ConnectAsync(new Uri("ws://127.0.0.1:1234"));
                await cli.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);

                buffer = new byte[4096];
                ValueWebSocketReceiveResult message = await cli.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);

                Response resp;
                using (var mem = new MemoryStream(buffer, 0, message.Count))
                {
                    var ser = MessagePackSerializer.Get<Response>();
                    resp = ser.Unpack(mem);
                }

                if (resp.Error != null)
                    throw new InvalidOperationException(resp.Error);


                Thread.Sleep(-1);
            }
        }
    }
}
