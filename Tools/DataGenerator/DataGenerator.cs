using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Bogus.DataSets;
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

        public async Task GenAsync()
        {
            using (var transaction = _provider.Transaction())
            {
                await GenUsers();

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

            var userDbs = userFaker.Generate(10);
            return _provider.BatchInsertAsync(userDbs);
        }
    }
}