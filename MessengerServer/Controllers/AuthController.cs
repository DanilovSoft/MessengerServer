using Contract;
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
using Contract.Dto;

namespace MessengerServer.Controllers
{
    public sealed class AuthController : ServerController, IAuthController
    {
        private readonly IDataProvider _dataProvider;

        public AuthController(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        [AllowAnonymous]
        public async Task<AuthorizationResult> Authorize(string login, string password)
        {
            var user = await _dataProvider.Get<UserDb>()
                .Where(x => 
                    x.NormalLogin == login.ToLower() &&
                    x.Password == PostgresEfExtensions.Crypt(password, x.Password)
                )
                .Select(x => new
                {
                    x.Id,
                    Name = x.Login,
                    ImageUrl = x.Profile.AvatarUrl,
                })
                .SingleOrDefaultAsync(Context.CancellationToken);

            if (user == null)
                throw new RemoteException("Не верный логин и/или пароль");

            // Авторизовываем текущее подключение.
            BearerToken token = Context.Authorize(userId: user.Id);

            Console.WriteLine($"Авторизован пользователь: \"{login}\"");

            return new AuthorizationResult
            {
                Token = token,
                UserId = user.Id,
                UserName = user.Name,
                ImageUrl = new Uri(user.ImageUrl)
            };
        }

        [AllowAnonymous]
        public bool AuthorizeToken(byte[] token)
        {
            return Context.AuthorizeToken(token);
        }
    }
}