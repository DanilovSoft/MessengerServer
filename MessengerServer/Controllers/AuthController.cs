using MessengerServer.Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessengerServer.Controllers
{
    internal class AuthController : IUnauthorized
    {
        public bool Authorize(string login, string password)
        {
            throw new NotImplementedException();
        }
    }
}
