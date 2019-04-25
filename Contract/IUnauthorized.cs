using System;
using System.Collections.Generic;
using System.Text;

namespace MessengerServer.Contract
{
    public interface IUnauthorized
    {
        bool Authorize(string login, string password);
    }
}
