using Contract;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Ninject.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            Dto.User user;
            using (var db = new ApplicationContext())
            {
                user = await db.Users
                    .Where(x =>
                        x.Name.ToLower() == login.ToLower() && x.Password == ApplicationContext.Crypt(password, x.Password)
                    )
                    .Select(x => new Dto.User
                    {
                        Id = x.Id
                    })
                .SingleOrDefaultAsync();
            }

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
