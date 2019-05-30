using Contract;
using DbModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DBCore;
using Dto;
using wRPC;
using Danilovsoft.MicroORM;
using Npgsql;
using System.Data.Common;

namespace MessengerServer.Controllers
{
    public sealed class HomeController : ServerController, IHomeController
    {
        private readonly IDataProvider _dataProvider;
        private readonly ILogger _logger;
        private readonly SqlORM _sql;

        public HomeController(IDataProvider dataProvider, ILogger<HomeController> logger, SqlORM sql)
        {
            _dataProvider = dataProvider;
            _sql = sql;
            _logger = logger;
        }

        // Возвращает список контактов пользователя.
        public async Task<ChatUser[]> GetConversations()
        {
            var groups = await _dataProvider
                .Get<UserGroupDb>()
                .Where(x => x.UserId == UserId) // Только чаты пользователя.
                .Select(x => x.Group)
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

        [ProducesProtoBuf]
        public async Task<SendMessageResult> SendMessage(string message, long groupId)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            _logger.LogInformation($"Получено сообщение: \"{message}\"");

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

            //            int[] users = await _sql.Sql(@"SELECT ug.""UserId"" 
            //FROM ""UserGroups"" ug 
            //WHERE ug.""GroupId"" = @group_id")
            //                    .Parameter("group_id", groupId)
            //                    .ToAsync()
            //                    .ScalarArray<int>();

            //await _sql.Sql(
            //    @"INSERT INTO public.""Messages"" (""Id"", ""CreatedUtc"", ""Text"", ""GroupId"", ""UserId"", ""UpdatedUtc"")
            //        SELECT @id, @created, @text, @group_id, @user_id, @updated_utc
            //          WHERE
            //            EXISTS(
            //                SELECT * FROM ""Groups"" g
            //                JOIN ""UserGroups"" ug ON ug.""GroupId"" = g.""Id""
            //                WHERE g.""Id"" = @group_id AND ug.""UserId"" = @sender
            //            )")
            //    .Parameter("id", messageDb.Id)
            //    .Parameter("created", messageDb.CreatedUtc)
            //    .Parameter("updated_utc", messageDb.CreatedUtc)
            //    .Parameter("text", message)
            //    .Parameter("group_id", groupId)
            //    .Parameter("user_id", UserId)
            //    .Parameter("sender", UserId)
            //    .ToAsync()
            //    .Execute();

            // Пользователи входящие в группу.
            int[] users = await _dataProvider
                .Get<GroupDb>()
                .Where(x => x.Users.Any(y => y.UserId == UserId)) // Группы в которые входит пользователь.
                .Where(x => x.Id == groupId)
                .SelectMany(x => x.Users)
                .Select(x => x.UserId)
                .ToArrayAsync();

            var result = new SendMessageResult
            {
                MessageId = messageDb.Id.ToByteArray(),
                Date = messageDb.CreatedUtc,
            };

            // Запись в бд.
            await _dataProvider.InsertAsync(messageDb);
            
            //ThreadPool.UnsafeQueueUserWorkItem(delegate 
            //{
            //    foreach (int userId in users.Except(new[] { UserId }))
            //    {
            //        // Находим подключение пользователя по его UserId.
            //        if (Context.Listener.Connections.TryGetValue(userId, out UserConnections connections))
            //        {
            //            // Отправить сообщение через все соединения пользователя.
            //            foreach (Context context in connections)
            //            {
            //                ThreadPool.UnsafeQueueUserWorkItem(async delegate
            //                {
            //                    var client = context.GetProxy<IClientController>();
            //                    try
            //                    {
            //                        await client.OnMessage(message, fromGroupId: groupId, id);
            //                    }
            //                    catch (Exception ex)
            //                    {
            //                        Debug.WriteLine(ex);
            //                    }
            //                }, null);
            //            }
            //        }
            //    }
            //}, null);

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
