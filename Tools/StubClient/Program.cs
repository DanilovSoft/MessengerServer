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
            SpinWait.SpinUntil(() => Mutex.TryOpenExisting($"MessengerServer_Port:{Port}", out mutex));
            mutex.Dispose();
            #endregion

            using (var client = new ClientConnection("127.0.0.1", Port))
            {
                var authController = client.GetProxy<IAuthController>();
                var homeController = client.GetProxy<IHomeController>();
                var utilsController = client.GetProxy<IUtilsController>();

                Console.WriteLine("Авторизация...");
                AuthorizationResult token = await authController.Authorize(login: "Test1", password: "123456");

                await utilsController.ShrinkImage(new ShrinkImageRequest
                {
                    ImageUri = new Uri("https://s3.amazonaws.com/uifaces/faces/twitter/adellecharles/128.jpg"),
                    Size = 120
                });

                var users = await homeController.GetConversations();

                while (true)
                {
                    Console.Write("Введите сообщение: ");
                    string line = Console.ReadLine();
                    await homeController.SendMessage(message: line, userId: 123456);
                }
            }
        }
    }
}
