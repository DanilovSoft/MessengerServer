using System;
using System.Threading;
using System.Threading.Tasks;
using vRPC;
using Contract;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using Dto;
using System.Diagnostics;
using System.Security;
using System.Runtime.InteropServices;
using System.Text;
using StubClient.Interfaces;
using System.Drawing;
using DanilovSoft.WebSocket;
using System.Net;

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

            using (var client = new ServerContext("127.0.0.1", Port))
            {
                client.ConfigureService(ioc => { });

                var authController = client.GetProxy<IAuthController>();
                var homeController = client.GetProxy<IHomeController>();
                var utilsController = client.GetProxy<IUtilsController>();
                var profileController = client.GetProxy<IProfileController>();

                //AuthorizationResult regResult = authController.Register("Test123456", "my_password").GetAwaiter().GetResult();
                
                Console.WriteLine("Авторизация...");
                //AuthorizationResult authorizationResult;
                //try
                //{
                //    authorizationResult = authController.Authorize(login: "Test123456", password: "my_password").GetAwaiter().GetResult();
                //}
                //catch (Exception ex)
                //{
                //    return;
                //}
                
                //client.BearerToken = authorizationResult.BearerToken.Key;

                //ChatUser[] groups = await homeController.GetConversations();
                //var user = groups[3];
                while (true)
                {
                    await homeController.TestMeAsync();

                    Thread.Sleep(-1);

                    //Console.Write("Введите сообдение: ");
                    //string line = Console.ReadLine();

                    //var sw = Stopwatch.StartNew();
                    //homeController.SendMessage(line, 1).GetAwaiter().GetResult();
                    //sw.Stop();

                    //Console.WriteLine($"Время: {sw.ElapsedMilliseconds} msec");
                }
            }
        }
    }
}
