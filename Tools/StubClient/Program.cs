using System;
using System.Threading;
using System.Threading.Tasks;
using wRPC;
using Contract;
using Contract.Dto;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace StubClient
{
    class Program
    {
        private const int Port = 65125;

        static async Task Main()
        {
            var mem = new MemoryPoolStream();
            for (int i = 0; i < 10; i++)
            {
                mem.WriteByte(1);
            }
            mem.Position = 0;
            mem.Read(new byte[3], 0, 3);
            mem.Seek(0, SeekOrigin.End);
            mem.Capacity = 20;

            var ar = new byte[10];
            Array.Fill<byte>(ar, 1);
            mem.Write(ar, 0, 10);

            Console.Title = "Клиент";

            #region Debug: Ждем запуск сервера

            Mutex mutex = null;
            Console.Write("Ожидаем процес сервера...");
            SpinWait.SpinUntil(() => Mutex.TryOpenExisting($"MessengerServer_Port:{Port}", out mutex));
            Console.WriteLine(" Ok");
            mutex.Dispose();

            #endregion

            using (var client = new ClientConnection("127.0.0.1", Port))
            {
                client.ConfigureService(ioc => { });

                var authController = client.GetProxy<IAuthController>();
                var homeController = client.GetProxy<IHomeController>();
                var utilsController = client.GetProxy<IUtilsController>();

                Console.WriteLine("Авторизация...");
                AuthorizationResult authorizationResult;
                try
                {
                    authorizationResult = await authController.Authorize(login: "Test2", password: "123456");
                }
                catch (Exception ex)
                {
                    throw;
                }
                
                client.BearerToken = authorizationResult.BearerToken.Key;

                //ChatUser[] groups = await homeController.GetConversations();
                //var user = groups[3];
                while (true)
                {
                    byte[] img = await utilsController.ShrinkImage(new Uri("https://s3.amazonaws.com/uifaces/faces/twitter/batsirai/128.jpg"), 183);
                    //File.WriteAllBytes("D:\\test.jpg", img);

                    Console.Write("Введите сообдение: ");
                    string line = Console.ReadLine();
                    await homeController.SendMessage(line, 1);
                }
            }
        }
    }
}
