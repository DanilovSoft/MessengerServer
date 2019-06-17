using Contract;
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
                _logger.LogWarning("Не верный логин и/или пароль");

                return BadRequest("Не верный логин и/или пароль");
                throw new RemoteException("Не верный логин и/или пароль");
            }

            // Авторизовываем текущее подключение.
            BearerToken bearerToken = Context.Authorize(userId: user.UserId);

            _logger.LogInformation($"Авторизован пользователь: \"{user.Login}\"");

            return Ok(new AuthorizationResult
            {
                BearerToken = bearerToken,
                UserId = user.UserId,
                UserName = user.Login,
                ImageUrl = new Uri(user.AvatarUrl)
            });
        }

        [AllowAnonymous]
        public void Register(string login, string password)
        {
            
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