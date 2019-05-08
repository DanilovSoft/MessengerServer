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
            Dto.User user = null;
            using (var db = new ApplicationContext())
            {
                var u = await db.Users
                    .Where(x =>
                        string.Equals(x.Name, login, StringComparison.InvariantCultureIgnoreCase)
                        && x.Password == ApplicationContext.Crypt(password, x.Password)
                    )
                    //.Select(x => new
                    //{
                    //    x.Id
                    //})
                .SingleOrDefaultAsync();

                //user = await db.Database.GetDbConnection()
                //.Query<Dto.User>(@"SELECT ""Id"" FROM ""Users"" WHERE ""Name"" = @Name AND ""Password"" = crypt(@Password, ""Password"")",
                //    param: new { Name = login, Password = password })
                //    .ToAsyncEnumerable()
                //    .FirstOrDefault();
            }


//            user = await db.Users.FromSql(@"SELECT ""Id""
//FROM ""Users""
//WHERE ""Name"" = {0} AND ""Password"" = crypt({1}, ""Password"")", login, password)
//    .Select(x => new Dto.User
//    {
//        Id = x.Id
//    })
//    .FirstOrDefaultAsync();
//        }

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
