using Contract;
using EfProvider;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wRPC;

namespace MessengerServer.Controllers
{
    public class HomeController : ServerController, IHomeController
    {
        private readonly IDataProvider _dataProvider;

        public HomeController(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        // Возвращает список контактов пользователя.
        public Task<ChatUser[]> GetConversations()
        {
            

            return Task.FromResult(Array.Empty<ChatUser>());
        }

        public Task SendMessage(string message, int userId)
        {
            Console.WriteLine($"Получено сообщение: \"{message}\"");

            // TODO: записать в БД.

            // Находим подключение пользователя по его UserId.
            if (Context.Listener.Connections.TryGetValue(userId, out UserConnections connections))
            {
                // Отправить сообщение через все соединения пользователя.
                foreach (Context context in connections)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(async delegate
                    {
                        var client = context.GetProxy<IClientController>();
                        try
                        {
                            await client.OnMessage(message, fromUserId: Context.UserId.Value);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }, null);
                }
            }
            return Task.CompletedTask;
        }
    }
}
