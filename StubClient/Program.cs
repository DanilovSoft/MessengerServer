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
using wRPC.Contract;
using Contract;
using wRPC;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace StubClient
{
    class Program
    {
        static async Task Main()
        {
            Console.Title = "Клиент";

            #region Debug: Ждем запуск сервера

            Mutex mutex = null;
            SpinWait.SpinUntil(() => Mutex.TryOpenExisting($"MessengerServer_Port:{1234}", out mutex));
            mutex.Dispose();
            #endregion

            Warmup.DoWarmup();
            using (var client = new Client())
            {
                var authController = client.GetProxy<IAuthController>("Auth");
                var homeController = client.GetProxy<IHomeController>("Home");

                Console.WriteLine("Авторизация...");
                await client.ConnectAsync("127.0.0.1", 1234);
                bool success = await authController.Authorize(login: "}{0ТТ@БЬ)Ч", password: "P@ssw0rd");
                while (true)
                {
                    Console.Write("Введите сообщение: ");
                    string line = Console.ReadLine();
                    string reply = await homeController.SendMessage(message: line, userId: 123456);
                    Console.WriteLine($"Ответ сервера: \"{reply}\"");
                }
            }
        }
    }
}
