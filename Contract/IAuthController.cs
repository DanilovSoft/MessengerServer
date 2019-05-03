using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC.Contract
{
    [ControllerContract("Auth")]
    public interface IAuthController
    {
        Task<bool> Authorize(string login, string password);
    }
}
