using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbModel;
using DbModel.DbTypes;
using EfProvider;

namespace DataGenerator
{
    public class DataGenerator
    {
        /// <summary>
        /// “естовые аватарки пользователей.
        /// </summary>
        private static readonly Uri[] _avatars;
        private readonly IDataProvider _provider;
        private readonly string _environment;

        static DataGenerator()
        {
            _avatars = new Uri[] 
            {
                new Uri("https://www.gravatar.com/avatar/205e460b479e2e5b48aec07710c08d50"),
                new Uri("https://gravatar.com/avatar/a4efdece4ec99c4aa3925c3f6ac48ba0?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/90ea7791778ab2e424ec92e864fe2461?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/4e0d1e2362b512e53d3c8d2ab1c313e0?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/6747271b27d79764e22e53e616b5bfe0?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/1d456003c8778fa4ded2bf87ffcf84a8?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/7a684d1210f62a69a82f7da7fd803f69?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/044d281ad4b445e3a53e4a8c59aebe7f?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/cf08151911c2d9aabc6f73310745ba33?s=400&d=robohash&r=x"),
                new Uri("https://gravatar.com/avatar/3f40a95c1d7acd45eed3989bfb69c6dc?s=400&d=robohash&r=x"),
            };
        }

        public DataGenerator(IDataProvider provider, string environment = default)
        {
            _provider = provider;
            _environment = environment;
        }

        public async Task GenAsync()
        {
            using (var transaction = _provider.Transaction())
            {
                await UserAsync();

                transaction.Commit();
            }
        }

        /// <summary>
        ///  —оздает тетстовых пользователей в базе.
        /// </summary>
        private Task UserAsync()
        {
            IEnumerable<UserDb> CreateUsers()
            {
                for (var i = 0; i < 10; i++)
                {
                    int id = i + 1;
                    var userDb = new UserDb
                    {
                        Id = id,
                        Login = $"Test{id}",
                        Password = "123456",
                        Profile = new UserProfileDb
                        {
                            //Id = id,
                            Gender = Gender.Undefined,
                            Avatar = _avatars[i % _avatars.Length].ToString()
                        }
                    };
                    userDb.NormalLogin = userDb.Login.ToLower();
                    yield return userDb;
                }
            }
            return _provider.BatchInsertAsync(CreateUsers());
        }
    }
}