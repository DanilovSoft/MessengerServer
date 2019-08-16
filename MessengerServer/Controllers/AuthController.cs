using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using DbModel;
using vRPC;
using DBCore;
using Microsoft.Extensions.Logging;
using DanilovSoft.MicroORM;
using System.Security;
using System.Text;
using Dto;

namespace MessengerServer.Controllers
{
    public sealed class AuthController : ServerController
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
        public async Task<IActionResult> Authorize(string login, string password)
        {
            AuthResult user = await _sql.Sql(@"
SELECT u.user_id, u.login, p.avatar_url, p.display_name
FROM users u
JOIN user_profiles p USING(user_id)
WHERE 
    LOWER(u.login) = @login 
    AND u.password = crypt(@pass, u.password)")
                .Parameter("login", login?.ToLower())
                .Parameter("pass", password)
                .ToAsync()
                .SingleOrDefault<AuthResult>();

            if (user == null)
            {
                _logger.LogWarning($"Не верный логин и/или пароль. Login: \"{login}\"");
                return BadRequest("Не верный логин и/или пароль.");
            }

            // Авторизовываем текущее подключение.
            BearerToken bearerToken = Context.Authorize(userId: user.UserId);

            _logger.LogInformation($"Авторизован пользователь: \"{user.Login}\"");

            return Ok(new AuthorizationResult
            {
                BearerToken = bearerToken,
                UserId = user.UserId,
                UserName = user.Login,
                ImageUrl = user.AvatarUrl == null ? null : new Uri(user.AvatarUrl)
            });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Register(string login, string password)
        {
            int? user_id;
            using (var t = _sql.Transaction())
            {
                await t.OpenTransactionAsync();

                user_id = await t.Sql(@"INSERT INTO users (login, password)
VALUES (@login, crypt(@password, gen_salt('bf')))
ON CONFLICT (login) DO NOTHING RETURNING user_id")
                    .Parameter("login", login)
                    .Parameter("password", password)
                    .ToAsync()
                    .ScalarOrDefault<int?>();

                if (user_id != null)
                {
                    await t.Sql("INSERT INTO user_profiles (user_id) VALUES (@user_id)")
                        .Parameter("user_id", user_id)
                        .ToAsync()
                        .Execute();

                    t.Commit();
                }
            }

            if (user_id != null)
            {
                _logger.LogInformation($"Зарегистрирован пользователь: \"{login}\"");

                // Авторизовываем текущее подключение.
                BearerToken bearerToken = Context.Authorize(user_id.Value);

                return Ok(new AuthorizationResult
                {
                    BearerToken = bearerToken,
                    UserId = user_id.Value,
                    UserName = null,
                    ImageUrl = null,
                });
            }
            return BadRequest($"Логин \"{login}\" уже занят.");
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

        //[AllowAnonymous]
        //public async Task<string> TestStop()
        //{
        //    await Task.Delay(1000);
        //    return "OK";
        //}
    }
}