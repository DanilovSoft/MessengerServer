using System;
using System.Threading.Tasks;
using vRPC;

namespace MessengerServer.Interfaces
{
    [ControllerContract("Home")]
    public interface IClientController
    {
        Task OnMessage(string message, long fromGroupId, Guid messageId);
        Task Typing(long groupId);
    }
}
