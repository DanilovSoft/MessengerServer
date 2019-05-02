using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC.Contract
{
    public interface IAuthController
    {
        Task<bool> Authorize(string login, string password);
    }
}
