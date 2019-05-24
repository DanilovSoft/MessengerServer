using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using DBCore;
using DbModel;
using DbModel.DbTypes;
using Microsoft.EntityFrameworkCore;

namespace DataGenerator
{
    public class DataGenerator
    {
        private readonly IDataProvider _provider;
        private readonly string _environment;

        public DataGenerator(IDataProvider provider, string environment = default)
        {
            _provider = provider;
            _environment = environment;
        }

        public async Task GenAsync()
        {
            using (var transaction = _provider.Transaction())
            {
                await GenUsers();
                await GenGroups();
                await GenMessages(5);
                await GenMessages(5);

                transaction.Commit();
            }
        }

        private Task GenUsers()
        {
            if (_provider.Get<UserDb>().Any()) return Task.CompletedTask;

            int id = 1;
            var profileFaker = new Faker<UserProfileDb>()
                .RuleFor(p => p.Gender, f => f.PickRandom<Gender>())
                .RuleFor(p => p.DisplayName, f => f.Name.FullName())
                .RuleFor(p => p.AvatarUrl, f => f.Internet.Avatar());

            var userFaker = new Faker<UserDb>()
                .CustomInstantiator(f => new UserDb {Id = id++})
                .RuleFor(p => p.Login, (f, u) => $"Test{u.Id}")
                .RuleFor(p => p.Password, "123456")
                .RuleFor(p => p.NormalLogin, (f, u) => u.Login.ToLower())
                .RuleFor(p => p.Profile, () => profileFaker);

            List<UserDb> userDbs = userFaker.Generate(10);
            return _provider.BatchInsertAsync(userDbs);
        }

        private async Task GenGroups()
        {
            if (await _provider.Get<GroupDb>().AnyAsync()) return;

            var userIds = await _provider.Get<UserDb>().Select(u => u.Id).ToArrayAsync();

            long id = 1;
            var groupFaker = new Faker<GroupDb>()
                .CustomInstantiator(f => new GroupDb {Id = id++})
                .RuleFor(p => p.Name, f => f.Company.CompanyName())
                .RuleFor(p => p.AvatarUrl, f => f.Internet.Avatar())
                .RuleFor(p => p.Users, () => new List<UserGroupDb>())
                .RuleFor(p => p.Messages, () => new List<MessageDb>());

            var group1 = Gen();
            var group2 = Gen(2);

            await _provider.BatchInsertAsync(group1.Union(group2));

            IEnumerable<GroupDb> Gen(int addingUsers = 1)
            {
                var groups = groupFaker.Generate(userIds.Length);
                for (int i = 0; i < userIds.Length; i++)
                {
                    var group = groups[i];
                    group.CreatorId = userIds[i];
                    group.Users.Add(new UserGroupDb {GroupId = group.Id, UserId = group.CreatorId});

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
        /// <param name="count">Количество сообщений от пользователя внутри каждого чата</param>
        /// <returns></returns>
        private async Task GenMessages(int count = 10)
        {
            var messageFaker = new Faker<MessageDb>()
                .RuleFor(p => p.Id, Guid.NewGuid)
                .RuleFor(p => p.Text, f => f.Random.Words())
                .RuleFor(p => p.FileUrl, f => f.Image.PicsumUrl());

            var userGroups = await _provider.Get<UserGroupDb>().ToArrayAsync();
            var messageDbs = messageFaker.Generate(count * userGroups.Length);
            for (var i = 0; i < userGroups.Length; i++)
            {
                var userGroup = userGroups[i];
                var messages = messageDbs.Skip(i * count).Take(count);
                foreach (var message in messages)
                {
                    message.UserId = userGroup.UserId;
                    message.GroupId = userGroup.GroupId;
                }
            }

            await _provider.BatchInsertAsync(messageDbs.OrderBy(x => Guid.NewGuid()));
        }
    }
}