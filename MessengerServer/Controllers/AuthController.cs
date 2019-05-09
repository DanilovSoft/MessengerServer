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
        private readonly IDataProvider _dataProvider;

        public AuthController(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        [AllowAnonymous]
        public async Task<BearerToken> Authorize(string login, string password)
        {
            Dto.User user = await _dataProvider.Get<UserDb>()
                .Where(x => 
                    x.NormalLogin == login.ToLower() &&
                    x.Pasword == PostgresEfExtensions.Crypt(password, x.Pasword)
                )
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
        public bool AuthorizeToken(byte[] token)
        {
            return Context.AuthorizeToken(token);
        }
    }
}