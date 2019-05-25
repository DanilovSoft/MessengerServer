using Contract;
using Contract.Dto;
using DbModel;
using EfProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger _logger;

        public HomeController(IDataProvider dataProvider, ILogger<HomeController> logger)
        {
            _dataProvider = dataProvider;
            _logger = logger;
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
                    GroupUserId = x.Users.Where(y => y.UserId != UserId).Select(y => y.UserId).FirstOrDefault(),
                })
                .ToArrayAsync();

            return groups.Select(x => new ChatUser
            {
                AvatarUrl = new Uri(x.Group.AvatarUrl),
                GroupId = x.Group.Id,
                Name = x.Group.Name,
                LastMessage = FromMessageDb(x.LastMessage),
                IsOnline = Context.Listener.Connections.ContainsKey(x.GroupUserId),
            }).ToArray();
        }

        private ChatMessage FromMessageDb(MessageDb message)
        {
            if (message == null)
                return null;

            return new ChatMessage
            {
                MessageId = message.Id,
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

        public async Task<SendMessageResult> SendMessage(string message, long groupId)
        {
            _logger.LogInformation($"Получено сообщение: \"{message}\"");

            return null;

            // Пользователи входящие в группу.
            int[] users = await _dataProvider
                .Get<GroupDb>()
                .Where(x => x.Users.Any(y => y.UserId == UserId)) // Группы в которые входит пользователь.
                .Where(x => x.Id == groupId)
                .SelectMany(x => x.Users)
                .Select(x => x.UserId)
                .ToArrayAsync();

            MessageDb messageDb = await _dataProvider.InsertAsync(new MessageDb
            {
                Text = message,
                GroupId = groupId,
                UserId = UserId,
            });

            var result = new SendMessageResult
            {
                MessageId = messageDb.Id.ToByteArray(),
                Date = messageDb.CreatedUtc,
            };

            foreach (int userId in users.Except(new[] { UserId }))
            {
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
                                await client.OnMessage(message, fromGroupId: groupId, messageDb.Id);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex);
                            }
                        }, null);
                    }
                }
            }

            return result;
        }

        public async Task Typing(long groupId)
        {
            _logger.LogInformation($"Пользователь {UserId} печатает.");

            // Пользователи входящие в группу.
            int[] users = await _dataProvider
                .Get<GroupDb>()
                .Where(x => x.Users.Any(y => y.UserId == UserId)) // Группы в которые входит пользователь.
                .Where(x => x.Id == groupId)
                .SelectMany(x => x.Users)
                .Select(x => x.UserId)
                .ToArrayAsync();

            foreach (int userId in users.Except(new[] { UserId }))
            {
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
                                await client.Typing(groupId);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex);
                            }
                        }, null);
                    }
                }
            }
        }
    }
}
