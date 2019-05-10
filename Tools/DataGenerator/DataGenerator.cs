using System.Collections.Generic;
using System.Threading.Tasks;
using DbModel;
using DbModel.DbTypes;
using EfProvider;

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

        public async Task Gen()
        {
            using (var transaction = _provider.Transaction())
            {
                await User().ConfigureAwait(false);
                transaction.Commit();
            }
        }

        private Task User()
        {
            var users = new List<UserDb>();
            for (var i = 0; i < 10; i++)
            {
                var userDb = new UserDb
                {
                    Login = "Test" + i,
                    Password = "123456",
                    Profile = new UserProfileDb {Gender = Gender.Undefined}
                };
                userDb.NormalLogin = userDb.Login.ToLower();
                users.Add(userDb);
            }

            return _provider.BatchInsert(users);
        }
    }
}