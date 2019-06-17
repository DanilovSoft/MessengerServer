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
using System.Data.Common;
using System.Collections.Generic;
using DanilovSoft.MicroORM;

namespace MessengerServer.Controllers
{
    public sealed class HomeController : ServerController, IHomeController
    {
        private readonly ILogger _logger;
        private readonly SqlORM _sql;

        public HomeController(ILogger<HomeController> logger, SqlORM sql)
        {
            _sql = sql;
            _logger = logger;
        }

        // Возвращает список контактов пользователя.
        public async Task<ChatUser[]> GetConversations()
        {
            // Группы в которые входит пользователь.
            var groups = _sql.Sql("SELECT group_id, name FROM user_groups WHERE user_id = @user_id")
                .Parameter("user_id", UserId)
                .List(new { group_id = 0L, name = "" });

            IEnumerable<long> groupIds = groups.Select(x => x.group_id).Distinct();

            var messages = _sql.Sql(@"SELECT message_id, group_id, user_id, text, created 
FROM messages
WHERE group_id = ANY(@group_ids)")
                .Parameter("group_ids", groupIds)
                .List<MessageDb>();

            foreach (var group in groups.Join(messages, x => x.group_id, y => y.GroupId, (x, y) => new { Group = x, Message = y }).GroupBy(x => x.Group.group_id))
            {
                
            }

            //var groups = await _dataProvider
            //    .Get<UserGroupDb>()
            //    .Where(x => x.UserId == UserId) // Только чаты пользователя.
            //    .Select(x => x.Group)
            //    .Select(x => new
            //    {
            //        Group = x,
            //        LastMessage = x.Messages.OrderByDescending(y => y.CreatedUtc).FirstOrDefault(),
            //        GroupUserId = x.Users.Where(y => y.UserId != UserId).Select(y => y.UserId).FirstOrDefault(),
            //    })
            //    .ToArrayAsync();

            throw new NotImplementedException();
            //return groups.Select(x => new ChatUser
            //{
            //    AvatarUrl = new Uri(x.Group.AvatarUrl),
            //    GroupId = x.Group.Id,
            //    Name = x.Group.Name,
            //    LastMessage = FromMessageDb(x.LastMessage),
            //    IsOnline = Context.Listener.Connections.ContainsKey(x.GroupUserId),
            //}).ToArray();
        }

        private ChatMessage FromMessageDb(MessageDb message)
        {
            if (message == null)
                return null;

            return new ChatMessage
            {
                MessageId = message.MessageId,
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
            throw new NotImplementedException();
            //IQueryable<MessageDb> query = _dataProvider
            //    .Get<GroupDb>()
            //    .Where(x => x.Id == chatId)
            //    .Where(x => x.Users.Any(y => y.UserId == Context.UserId.Value)) // Убедиться что пользователь входит в этот чат.
            //    .SelectMany(x => x.Messages)
            //    .OrderByDescending(x => x.CreatedUtc);

            //if(topMessageDate != null)
            //{
            //    query = query.Where(x => x.CreatedUtc > topMessageDate.Value);
            //}

            //var messages = await query
            //    .Take(count)
            //    .ToArrayAsync();

            //return messages.Select(x => FromMessageDb(x)).ToArray();
        }

        [ProducesProtoBuf]
        public async Task<SendMessageResult> SendMessage(string message, long groupId)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            _logger.LogInformation($"Получено сообщение: \"{message}\"");

            int[] users = _sql.Sql(@"SELECT ug.user_id 
            FROM user_groups ug 
            WHERE ug.group_id = @group_id")
                    .Parameter("group_id", groupId)
                    .ScalarArray<int>();

            var messageId = Guid.NewGuid();
            DateTime createdUtc = DateTime.UtcNow;

            _sql.Sql(
                @"INSERT INTO messages (message_id, created, text, group_id, user_id)
                    SELECT @message_id, @created, @text, @group_id, @user_id
                      WHERE
                        EXISTS(
                            SELECT * FROM groups g
                            JOIN user_groups ug USING(group_id)
                            WHERE g.group_id = @group_id AND ug.user_id = @sender
                        )")
                .Parameter("message_id", messageId)
                .Parameter("created", createdUtc)
                .Parameter("text", message)
                .Parameter("group_id", groupId)
                .Parameter("user_id", UserId)
                .Parameter("sender", UserId)
                .Execute();

            var result = new SendMessageResult
            {
                MessageId = messageId.ToByteArray(),
                Date = createdUtc,
            };

            ThreadPool.UnsafeQueueUserWorkItem(delegate
            {
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
                                    await client.OnMessage(message, fromGroupId: groupId, messageId);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex);
                                }
                            }, null);
                        }
                    }
                }
            }, null);

            return result;
        }

        public async Task Typing(long groupId)
        {
            _logger.LogInformation($"Пользователь {UserId} печатает.");

            // Пользователи входящие в группу.
            //int[] users = await _dataProvider
            //    .Get<GroupDb>()
            //    .Where(x => x.Users.Any(y => y.UserId == UserId)) // Группы в которые входит пользователь.
            //    .Where(x => x.Id == groupId)
            //    .SelectMany(x => x.Users)
            //    .Select(x => x.UserId)
            //    .ToArrayAsync();

            //foreach (int userId in users.Except(new[] { UserId }))
            //{
            //    // Находим подключение пользователя по его UserId.
            //    if (Context.Listener.Connections.TryGetValue(userId, out UserConnections connections))
            //    {
            //        // Отправить сообщение через все соединения пользователя.
            //        foreach (Context context in connections)
            //        {
            //            ThreadPool.UnsafeQueueUserWorkItem(async delegate
            //            {
            //                var client = context.GetProxy<IClientController>();
            //                try
            //                {
            //                    await client.Typing(groupId);
            //                }
            //                catch (Exception ex)
            //                {
            //                    Debug.WriteLine(ex);
            //                }
            //            }, null);
            //        }
            //    }
            //}
        }
    }
}
