using System;
using System.Threading;
using System.Threading.Tasks;
using wRPC;
using Contract;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using Dto;
using System.Diagnostics;
using System.Security;
using System.Runtime.InteropServices;
using System.Text;

namespace StubClient
{
    class Program
    {
        private const int Port = 65125;

        static void Main()
        {
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

                //AuthorizationResult regResult = authController.Register("Test123456", "my_password").GetAwaiter().GetResult();
                
                Console.WriteLine("Авторизация...");
                AuthorizationResult authorizationResult;
                try
                {
                    authorizationResult = authController.Authorize(login: "Test123456", password: "my_password").GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    return;
                }
                
                client.BearerToken = authorizationResult.BearerToken.Key;

                //ChatUser[] groups = await homeController.GetConversations();
                //var user = groups[3];
                while (true)
                {
                    homeController.GetConversations().GetAwaiter().GetResult();
                    //byte[] img = await utilsController.ShrinkImage(new Uri("https://s3.amazonaws.com/uifaces/faces/twitter/batsirai/128.jpg"), 183);
                    //File.WriteAllBytes("D:\\test.jpg", img);

                    Console.Write("Введите сообдение: ");
                    string line = Console.ReadLine();

                    var sw = Stopwatch.StartNew();
                    homeController.SendMessage(line, 1).GetAwaiter().GetResult();
                    sw.Stop();

                    Console.WriteLine($"Время: {sw.ElapsedMilliseconds} msec");
                }
            }
        }
    }
}
