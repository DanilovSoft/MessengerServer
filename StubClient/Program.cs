using Contract;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace StubClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var cli = new DanilovSoft.WebSocket.ClientWebSocket())
            {
                var request = new Request
                {
                    ActionName = "Authentication"
                };

                byte[] buffer;
                using (var mem = new MemoryStream())
                {
                    ProtoBuf.Serializer.Serialize(mem, request);
                    buffer = mem.ToArray();
                }

                Thread.Sleep(1000);
                await cli.ConnectAsync(new Uri("ws://127.0.0.1:1234"));
                await cli.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
                Thread.Sleep(-1);
            }
        }
    }
}
