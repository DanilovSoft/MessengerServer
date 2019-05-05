using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC.Contract
{
    [ControllerContract("Auth")]
    public interface IAuthController
    {
        Task<BearerToken> Authorize(string login, string password);
    }
}
