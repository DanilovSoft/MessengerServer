using Contract.Dto;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace Contract
{
    [ControllerContract("Home")]
    public interface IHomeController
    {
        Task SendMessage(string message, int userId);
        Task<ChatUser[]> GetConversations();
    }
}
