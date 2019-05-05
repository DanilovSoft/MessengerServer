using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public Task<BearerToken> Authorize(string login, string password)
        {
            // Авторизовываем текущее подключение.
            BearerToken token = Context.Authorize(userId: 123456);

            Console.WriteLine($"Авторизован пользователь: \"{login}\"");

            return Task.FromResult(token);
        }

        [AllowAnonymous]
        public void AuthorizeToken(byte[] token)
        {
            Context.AuthorizeToken(token);
        }
    }
}
