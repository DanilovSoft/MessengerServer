using Contract;
using Contract.Dto;
using DbModel;
using EfProvider;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wRPC;

namespace MessengerServer.Controllers
{
    public sealed class HomeController : ServerController, IHomeController
    {
        private readonly IDataProvider _dataProvider;

        public HomeController(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        // Возвращает список контактов пользователя.
        public async Task<ChatUser[]> GetConversations()
        {
            var groups = await _dataProvider
                .Get<UserGroupDb>()
                .Where(x => x.UserId == Context.UserId.Value)
                .Select(x => x.Group)
                .Include(x => x.Messages)
                .Select(x => new
                {
                    Group = x,
                    LastMessage = x.Messages.OrderBy(y => y.CreatedUtc).LastOrDefault(),
                })
                .ToArrayAsync();

            return groups.Select(x => new ChatUser
            {
                AvatarUrl = new Uri(x.Group.AvatarUrl),
                ChatId = x.Group.Id,
                Name = x.Group.Name,
                LastMessage = FromMessageDb(x.LastMessage),
            }).ToArray();
        }

        private ChatMessage FromMessageDb(MessageDb message)
        {
            if (message == null)
                return null;

            return new ChatMessage
            {
                Text = message.Text,
            };
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
