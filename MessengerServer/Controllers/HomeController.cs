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
                .Where(x => x.UserId == UserId) // Только чаты пользователя.
                .Select(x => x.Group)
                .Include(x => x.Messages)
                .Include(x => x.Users)
                .Select(x => new
                {
                    Group = x,
                    LastMessage = x.Messages.OrderByDescending(y => y.CreatedUtc).FirstOrDefault(),
                })
                .ToArrayAsync();

            return groups.Select(x => new ChatUser
            {
                UserId = 0, // Ид собеседника
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
                CreatedUtcDate = message.CreatedUtc,
                IsMy = message.UserId == UserId,
            };
        }

        /// <summary>
        /// Запрос пользователя на загрузку части истории сообщений.
        /// </summary>
        /// <param name="chatId">Идентификатор чата пользователя.</param>
        /// <param name="count">Сколько сообщений нужно загрузить.</param>
        /// <param name="topMessageDate">Дата верхнего сообщения в истории от которого нужно начать загрузку.</param>
        public async Task<ChatMessage[]> GetHistory(long chatId, int count, DateTime? topMessageDate)
        {
            IQueryable<MessageDb> query = _dataProvider
                .Get<GroupDb>()
                .Where(x => x.Id == chatId)
                .Where(x => x.Users.Any(y => y.UserId == Context.UserId.Value)) // Убедиться что пользователь входит в этот чат.
                .SelectMany(x => x.Messages)
                .OrderByDescending(x => x.CreatedUtc);

            if(topMessageDate != null)
            {
                query = query.Where(x => x.CreatedUtc > topMessageDate.Value);
            }

            var messages = await query
                .Take(count)
                .ToArrayAsync();

            return messages.Select(x => FromMessageDb(x)).ToArray();
        }

        public async Task<SendMessageResult> SendMessage(string message, int userId)
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

            throw new NotImplementedException();
        }
    }
}
