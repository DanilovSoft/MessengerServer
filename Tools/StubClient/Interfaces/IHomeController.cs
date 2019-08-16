using System;
using System.Threading.Tasks;
using Dto;
using vRPC;

namespace Contract
{
    [ControllerContract("Home")]
    public interface IHomeController
    {
        Task<SendMessageResult> SendMessage(string message, long groupId);
        Task<ChatUser[]> GetConversations();
        Task<ChatMessage[]> GetHistory(long chatId, int count, DateTime? topMessageDate);
        Task Typing(long groupId);
        Task TestMeAsync();
    }
}
