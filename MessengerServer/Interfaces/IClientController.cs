using System;
using System.Threading.Tasks;
using wRPC;

namespace MessengerServer.Interfaces
{
    [ControllerContract("Home")]
    public interface IClientController
    {
        Task OnMessage(string message, long fromGroupId, Guid messageId);
        Task Typing(long groupId);
    }
}
