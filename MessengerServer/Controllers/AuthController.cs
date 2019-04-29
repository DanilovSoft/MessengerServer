using MessengerServer.Contract;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace MessengerServer.Controllers
{
    internal class AuthController : BaseController
    {
        public Task<bool> Authorize(string login, string password)
        {
            return Task.FromResult(true);
        }
    }
}
