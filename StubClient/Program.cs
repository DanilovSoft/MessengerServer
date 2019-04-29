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
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace StubClient
{
    class Program
    {
        static async Task Main()
        {
            Mutex mutex = null;
            SpinWait.SpinUntil(() => Mutex.TryOpenExisting($"MessengerServer_Port:{1234}", out mutex));
            mutex.Dispose();

            using (var cli = new MyClientWebSocket())
            {
                var request = new Request
                {
                    ActionName = "Auth/Authorize",
                    Args = new[]
                    {
                        new Request.Arg("Login", "}{0ТТ@БЬ)Ч"),
                        new Request.Arg("Password", "P@ssw0rd")
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

                Console.WriteLine("Авторизация...");
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

                resp.EnsureSuccessStatusCode();

                while (true)
                {
                    Console.Write("Введите сообщение: ");
                    string line = Console.ReadLine();
                    await SendMessageAsync(cli, line);
                }
            }
        }

        private static async Task SendMessageAsync(MyClientWebSocket cli, string messageText)
        {
            var request = new Request
            {
                ActionName = "SendMessage",
                Args = new[]
                {
                    new Request.Arg("userId", MessagePackObject.FromObject(123456)),
                    new Request.Arg("message", messageText),
                }
            };

            byte[] buffer;
            using (var mem = new MemoryStream())
            {
                var ser = MessagePackSerializer.Get<Request>();
                ser.Pack(mem, request);
                buffer = mem.ToArray();
            }

            await cli.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);

            buffer = new byte[4096];
            ValueWebSocketReceiveResult message = await cli.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);

            Response resp;
            using (var mem = new MemoryStream(buffer, 0, message.Count))
            {
                var ser = MessagePackSerializer.Get<Response>();
                resp = ser.Unpack(mem);
            }

            resp.EnsureSuccessStatusCode();

            string respMessage = resp.Result.ToString();
            Console.WriteLine($"Ответ сервера: \"{respMessage}\"");
        }
    }
}
