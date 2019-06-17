using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using DanilovSoft.MicroORM;
using DBCore;
using DbModel;
using DbModel.DbTypes;
using Microsoft.EntityFrameworkCore;

namespace DataGenerator
{
    public class DataGenerator
    {
        private readonly SqlORM _sql;
        private readonly string _environment;

        public DataGenerator(SqlORM sql, string environment = default)
        {
            _sql = sql;
            _environment = environment;
        }

        public void Gen()
        {
            using (SqlTransaction transaction = _sql.Transaction())
            {
                transaction.OpenTransaction();

                GenUsers(transaction);
                GenGroups(transaction);
                GenMessages(transaction, 5);
                GenMessages(transaction, 5);

                transaction.Commit();
            }
        }

        private void GenUsers(SqlTransaction transaction)
        {
            bool hasAny = transaction.Sql("SELECT EXISTS (SELECT * FROM users)").Scalar<bool>();
            if (hasAny)
                return;

            int id = 1;
            int idProfile = 1;


            var userFaker = new Faker<UserDb>()
                .CustomInstantiator(f => new UserDb { Id = id++ })
                .RuleFor(p => p.Login, (f, u) => $"Test{u.Id}")
                .RuleFor(p => p.Password, "123456");
                //.RuleFor(p => p.Profile, () => profileFaker);

            var profileFaker = new Faker<UserProfileDb>()
                .CustomInstantiator(f => new UserProfileDb { Id = idProfile++ })
                .RuleFor(p => p.Gender, f => f.PickRandom<Gender>())
                .RuleFor(p => p.DisplayName, f => f.Name.FullName())
                .RuleFor(p => p.AvatarUrl, f => f.Internet.Avatar());

            List<UserDb> userDbs = userFaker.Generate(10);
            List<UserProfileDb> userProfiles = profileFaker.Generate(10);

            for (int i = 0; i < userDbs.Count; i++)
            {
                var user = userDbs[i];
                var profile = userProfiles[i];

                transaction.Sql("INSERT INTO users (user_id, login, password) VALUES (@user_id, @login, crypt(@pass, gen_salt('bf')))")
                    .Parameter("user_id", user.Id)
                    .Parameter("login", user.Login)
                    .Parameter("pass", user.Password)
                    .Execute();

                transaction.Sql("INSERT INTO user_profiles (user_id, avatar_url, display_name) VALUES (@id, @av, @disp)")
                    .Parameter("id", profile.Id)
                    .Parameter("av", profile.AvatarUrl)
                    .Parameter("disp", profile.DisplayName)
                    .Execute();
            }
        }

        private void GenGroups(SqlTransaction transaction)
        {
            bool hasAny = transaction.Sql("SELECT EXISTS (SELECT * FROM user_groups)").Scalar<bool>();
            if (hasAny)
                return;

            int[] userIds = transaction.Sql("SELECT user_id FROM users").ScalarArray<int>();

            long id = 1;
            var groupFaker = new Faker<GroupDb>()
                .CustomInstantiator(f => new GroupDb { Id = id++ })
                .RuleFor(p => p.Name, f => f.Company.CompanyName())
                .RuleFor(p => p.AvatarUrl, f => f.Internet.Avatar())
                .RuleFor(p => p.Users, () => new List<UserGroupDb>())
                .RuleFor(p => p.Messages, () => new List<MessageDb>());

            var group1 = Gen(1);
            var group2 = Gen(2);

            foreach (var group in group1.Union(group2))
            {
                transaction.Sql("INSERT INTO groups (group_id, creator_id, name, avatar_url) VALUES (@group_id, @creator_id, @name, @avatar_url)")
                    .Parameter("group_id", group.Id)
                    .Parameter("creator_id", group.CreatorId)
                    .Parameter("name", group.Name)
                    .Parameter("avatar_url", group.AvatarUrl)
                    .Execute();

                foreach (var item in group.Users)
                {
                    transaction.Sql("INSERT INTO user_groups (group_id, user_id, inviter_id) VALUES (@group_id, @user_id, @inviter_id)")
                       .Parameter("group_id", item.GroupId)
                       .Parameter("user_id", item.UserId)
                       .Parameter("inviter_id", item.InviterId)
                       .Execute();
                }
            }

            IEnumerable<GroupDb> Gen(int addingUsers)
            {
                var groups = groupFaker.Generate(userIds.Length);
                for (int i = 0; i < userIds.Length; i++)
                {
                    var group = groups[i];
                    group.CreatorId = userIds[i];
                    group.Users.Add(new UserGroupDb { GroupId = group.Id, UserId = group.CreatorId });

                    foreach (var userGroup in GenUserGroups(group, i, addingUsers))
                    {
                        group.Users.Add(userGroup);
                    }

                    yield return group;
                }
            }

            IEnumerable<UserGroupDb> GenUserGroups(GroupDb group, int current, int addingUsers = 1)
            {
                for (int i = 1; i <= addingUsers; i++)
                {
                    yield return new UserGroupDb
                    {
                        GroupId = group.Id,
                        UserId = userIds[(current + i >= userIds.Length ? current - userIds.Length : current) + i],
                        InviterId = group.CreatorId
                    };
                }
            }
        }

        /// <summary>
        /// Генерирует сообщения во все чаты от всех пользователей.
        /// Не делает проверку на существование других сообщений.
        /// </summary>
        /// <param name="count">Количество сообщений от пользователя внутри каждого чата.</param>
        /// <returns></returns>
        private void GenMessages(SqlTransaction transaction, int count = 10)
        {
            var messageFaker = new Faker<MessageDb>()
                .RuleFor(p => p.MessageId, Guid.NewGuid)
                .RuleFor(p => p.Text, f => f.Random.Words())
                .RuleFor(p => p.CreatedUtc, f => f.Date.Recent())
                .RuleFor(p => p.FileUrl, f => f.Image.PicsumUrl());

            var userGroups = _sql.Sql("SELECT user_id, group_id FROM user_groups").List<UserGroupDb>();

            //var userGroups = await _provider.Get<UserGroupDb>().ToArrayAsync();

            var messageDbs = messageFaker.Generate(count * userGroups.Count);
            for (var i = 0; i < userGroups.Count; i++)
            {
                var userGroup = userGroups[i];
                var messages = messageDbs.Skip(i * count).Take(count);
                foreach (var message in messages)
                {
                    message.UserId = userGroup.UserId;
                    message.GroupId = userGroup.GroupId;
                }
            }

            foreach (var message in messageDbs.OrderBy(x => Guid.NewGuid()))
            {
                _sql.Sql("INSERT INTO messages (message_id, group_id, user_id, text, created) VALUES (@message_id, @group_id, @user_id, @text, @created)")
                    .Parameter("message_id", message.MessageId)
                    .Parameter("group_id", message.GroupId)
                    .Parameter("user_id", message.UserId)
                    .Parameter("text", message.Text)
                    .Parameter("created", message.CreatedUtc)
                    .Execute();
            }
        }
    }
}