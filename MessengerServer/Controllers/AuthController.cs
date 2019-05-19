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
using Microsoft.Extensions.Logging;

namespace MessengerServer.Controllers
{
    public sealed class AuthController : ServerController, IAuthController
    {
        private readonly IDataProvider _dataProvider;
        private readonly ILogger _logger;

        public AuthController(IDataProvider dataProvider, ILogger<AuthController> logger)
        {
            _dataProvider = dataProvider;
            _logger = logger;
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
            {
                _logger.LogWarning("Не верный логин и/или пароль");
                throw new RemoteException("Не верный логин и/или пароль");
            }

            // Авторизовываем текущее подключение.
            BearerToken bearerToken = Context.Authorize(userId: user.Id);

            _logger.LogInformation($"Авторизован пользователь: \"{login}\"");

            return new AuthorizationResult
            {
                BearerToken = bearerToken,
                UserId = user.Id,
                UserName = user.Name,
                ImageUrl = new Uri(user.ImageUrl)
            };
        }

        [AllowAnonymous]
        public bool AuthorizeToken(byte[] token)
        {
            bool authorized = Context.AuthorizeToken(token);

            if(authorized)
            {
                _logger.LogInformation($"Авторизован пользователь {UserId} по токену.");
            }

            return authorized;
        }
    }
}