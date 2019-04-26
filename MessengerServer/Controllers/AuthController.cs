using MessengerServer.Contract;
using System;
using System.Collections.Generic;
using System.Text;
using wRPC;

namespace MessengerServer.Controllers
{
    internal class AuthController : BaseController, IUnauthorized
    {
        public bool Authorize(string login, string password)
        {
            return true;
        }
    }
}
