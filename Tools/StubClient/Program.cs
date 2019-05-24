using System;
using System.Threading;
using System.Threading.Tasks;
using wRPC;
using Contract;
using Contract.Dto;

namespace StubClient
{
    class Program
    {
        private const int Port = 65125;

        static async Task Main()
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
                    //byte[] img = await utilsController.ShrinkImage(token.ImageUrl, 320);
                    //File.WriteAllBytes("D:\\test.jpg", img);

                    Console.Write("Введите сообдение: ");
                    string line = Console.ReadLine();
                    await homeController.SendMessage(line, 1);
                }
            }
        }
    }
}
