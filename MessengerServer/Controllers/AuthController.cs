﻿using Contract;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using DbModel;
using EfProvider;
using wRPC;
using DBCore;
using Dto;
using Microsoft.Extensions.Logging;
using DanilovSoft.MicroORM;

namespace MessengerServer.Controllers
{
    public sealed class AuthController : ServerController, IAuthController
    {
        private readonly SqlORM _sql;
        private readonly ILogger _logger;

        public AuthController(SqlORM sql, ILogger<AuthController> logger)
        {
            _sql = sql;
            _logger = logger;
        }

        [AllowAnonymous]
        [ProducesProtoBuf]
        public async Task<AuthorizationResult> Authorize(string login, string password)
        {
            var user = _sql.Sql(@"
SELECT u.user_id, u.login, p.avatar_url, p.display_name
FROM users u
JOIN user_profiles p USING(user_id)
WHERE 
    LOWER(u.login) = @login 
    AND u.password = crypt(@pass, u.password)")
                .Parameter("login", login?.ToLower())
                .Parameter("pass", password)
                .SingleOrDefault(new { id = 0, login = "", avatar_url = "", display_name = "" });

            //var user = await _dataProvider.Get<UserDb>()
            //    .Where(x => 
            //        x.NormalLogin == login.ToLower() &&
            //        x.Password == PostgresEfExtensions.Crypt(password, x.Password)
            //    )
            //    .Select(x => new
            //    {
            //        x.Id,
            //        Name = x.Login,
            //        ImageUrl = x.Profile.AvatarUrl,
            //    })
            //    .FirstOrDefaultAsync(Context.CancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Не верный логин и/или пароль");
                throw new RemoteException("Не верный логин и/или пароль");
            }

            // Авторизовываем текущее подключение.
            BearerToken bearerToken = Context.Authorize(userId: user.id);

            _logger.LogInformation($"Авторизован пользователь: \"{user.login}\"");

            return new AuthorizationResult
            {
                BearerToken = bearerToken,
                UserId = user.id,
                UserName = user.login,
                ImageUrl = new Uri(user.avatar_url)
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