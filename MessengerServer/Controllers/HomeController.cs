using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wRPC;
using wRPC.Contract;

namespace MessengerServer.Controllers
{
    public class HomeController : Controller, IHomeController
    {
        public HomeController(ISqlContext sql)
        {
           
        }

        public Task<string> SendMessage(string message, int userId)
        {
            Console.WriteLine($"Получено сообщение: \"{message}\"");

            // TODO: записать в БД.

            // Находим подключение пользователя по его UserId.
            if (Connections.TryGetValue(userId, out UserConnections connections))
            {
                // Отправить сообщение через все соединения пользователя.
                foreach (Context context in connections)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(async delegate
                    {
                        try
                        {
                            await context.SendMessageAsync(fromUserId: Context.UserId.Value, message);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }, null);
                }
            }
            return Task.FromResult(message);
        }

        public void SyncTest()
        {
            
        }

        public Task SyncTestAsync()
        {
            return Task.CompletedTask;
        }
    }
}
