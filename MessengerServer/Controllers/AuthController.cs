using Contract;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DbModel;
using DbModel.Store;
using EfProvider;
using wRPC;
using wRPC.Contract;

namespace MessengerServer.Controllers
{
    internal class AuthController : ServerController, IAuthController
    {
        public AuthController()
        {
            
        }

        [AllowAnonymous]
        public async Task<BearerToken> Authorize(string login, string password)
        {
            var modelStore = new ModelStore();
            var builder = new DbContextOptionsBuilder<CustomEfDbContext>();
            builder.UseNpgsql("Server=where.now.im;Port=5432;User Id=postgres;Password=pizdec;Database=MessengerServer;Pooling=true;MinPoolSize=15;MaxPoolSize=20;CommandTimeout=20;Timeout=20");

            var context = new CustomEfDbContext(modelStore, builder.Options);
            var provider = new EfDataProvider(context);
            
            var user = await provider.Get<UserDb>()
                .Where(x => x.NormalLogin == login.ToLower() &&
                            x.Pasword == CustomEfDbContext.Crypt(password, x.Pasword))
                .Select(x => new Dto.User
                {
                    Id = x.Id,
                    Name = x.Login
                })
                .SingleOrDefaultAsync();
            

            if (user == null)
                throw new RemoteException("Не верный логин и/или пароль");

            // Авторизовываем текущее подключение.
            BearerToken token = Context.Authorize(userId: user.Id);

            Console.WriteLine($"Авторизован пользователь: \"{login}\"");

            return token;
        }

        [AllowAnonymous]
        public void AuthorizeToken(byte[] token)
        {
            Context.AuthorizeToken(token);
        }
    }
}