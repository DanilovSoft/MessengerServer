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
    internal class AuthController : Controller, IAuthController
    {
        public AuthController(ISqlContext sqlContext)
        {
            
        }

        [AllowAnonymous]
        public Task<bool> Authorize(string login, string password)
        {
            // Авторизовываем текущее подключение.
            Context.Authorize(userId: 123456);

            Console.WriteLine($"Авторизован пользователь: \"{login}\"");

            return Task.FromResult(true);
        }
    }
}
