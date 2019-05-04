using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC.Contract
{
    [ControllerContract("Home")]
    public interface IClientController
    {
        Task OnMessage(string message, int fromUserId);
    }
}
