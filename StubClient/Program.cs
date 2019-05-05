using System;
using System.Threading;
using System.Threading.Tasks;
using wRPC.Contract;
using wRPC;

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

            using (var client = new Client("127.0.0.1", 1234))
            {
                var authController = client.GetProxy<IAuthController>();
                var homeController = client.GetProxy<IHomeController>();

                Console.WriteLine("Авторизация...");
                BearerToken token = await authController.Authorize(login: "User1", password: "Passw0rd");
                client.BearerToken = token.Token;

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
