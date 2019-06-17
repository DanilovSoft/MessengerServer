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
using System.Text;
using MessengerServer.Interfaces;

namespace MessengerServer.Controllers
{
    public sealed class HomeController : ServerController
    {
        private readonly ILogger _logger;
        private readonly SqlORM _sql;

        public HomeController(ILogger<HomeController> logger, SqlORM sql)
        {
            _sql = sql;
            _logger = logger;
        }

        // Возвращает список контактов пользователя.
        [ProducesProtoBuf]
        public ChatUser[] GetConversations()
        {
            // Список контактов пользователя.
            var groups = _sql.Sql(@"SELECT g.group_id, g.name, g.avatar_url, t.message_id, t.user_id = @user_id AS is_my, t.text, t.created
FROM(SELECT m.*, row_number() OVER(PARTITION BY m.group_id ORDER BY m.created DESC) row_num FROM messages m)t
       JOIN groups g ON g.group_id = t.group_id
       JOIN user_groups ug on g.group_id = ug.group_id AND ug.user_id = @user_id
WHERE row_num = 1
ORDER BY t.created DESC")
                .Parameter("user_id", UserId)
                .List<UserConversation>();

            return groups.Select(x => new ChatUser
            {
                AvatarUrl = new Uri(x.AvatarUrl),
                GroupId = x.GroupId,
                Name = x.GroupName,
                LastMessage = new ChatMessage
                {
                    MessageId = x.MessageId,
                    Text = x.MessageText,
                    CreatedUtcDate = x.Created,
                    IsMy = x.IsMy
                },
                IsOnline = false, //Context.Listener.Connections.ContainsKey(x.GroupId),
            }).ToArray();
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
        public ChatMessage[] GetHistory(long chatId, int count, DateTime? topMessageDate)
        {
            var sb = new StringBuilder(@"
SELECT m.message_id, m.text, m.user_id, m.created
FROM messages m 
WHERE
EXISTS(SELECT 1 FROM user_groups ug WHERE ug.group_id = @group_id AND ug.user_id = @user_id) -- проверка вхождения пользователя в группу.
AND m.group_id = @group_id");

            if (topMessageDate != null)
            {
                sb.Append(" AND m.created > @top_message_date");
            }

            sb.Append(" ORDER BY m.created DESC LIMIT @limit");

            var messages = _sql.Sql(sb.ToString())
                .Parameter("group_id", chatId)
                .Parameter("user_id", UserId)
                .Parameter("limit", count)
                .Parameter("top_message_date", topMessageDate)
                .List<MessageDb>();

            return messages.Select(x => FromMessageDb(x)).Reverse().ToArray();
        }

        [ProducesProtoBuf]
        public SendMessageResult SendMessage(string message, long groupId)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            _logger.LogInformation($"Получено сообщение: \"{message}\"");

            // Все пользователи входящие в группу.
            int[] users = _sql.Sql(@"SELECT ug.user_id 
            FROM user_groups ug 
            WHERE ug.group_id = @group_id")
                    .Parameter("group_id", groupId)
                    .ScalarArray<int>();

            if (!users.Contains(UserId))
                return null; // Текущий пользователь не входит в группу.

            var messageId = Guid.NewGuid();
            DateTime createdUtc = DateTime.UtcNow;

            _sql.Sql(
                @"INSERT INTO messages (message_id, created, text, group_id, user_id) VALUES(@message_id, @created, @text, @group_id, @user_id)")
                .Parameter("message_id", messageId)
                .Parameter("created", createdUtc)
                .Parameter("text", message)
                .Parameter("group_id", groupId)
                .Parameter("user_id", UserId)
                .Execute();

            ThreadPool.UnsafeQueueUserWorkItem(delegate
            {
                // Отправить всем кроме себя.
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

            return new SendMessageResult
            {
                MessageId = messageId.ToByteArray(),
                Date = createdUtc,
            };
        }

        public void Typing(long groupId)
        {
            _logger.LogInformation($"Пользователь {UserId} печатает.");

            // Все пользователи входящие в группу.
            int[] users = _sql.Sql(@"SELECT ug.user_id 
            FROM user_groups ug 
            WHERE ug.group_id = @group_id")
                    .Parameter("group_id", groupId)
                    .ScalarArray<int>();

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
