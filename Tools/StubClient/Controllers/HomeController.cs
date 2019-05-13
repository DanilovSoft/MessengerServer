using Contract;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace StubClient.Controllers
{
    public class HomeController : ClientController, IClientController
    {
        public HomeController()
        {
            
        }

        public Task OnMessage(string message, long fromGroupId, Guid messageId)
        {
            Console.WriteLine($"Входное сообщение: {message}");
            return Task.CompletedTask;
        }

        public Task Typing(long groupId)
        {
            return Task.CompletedTask;
        }
    }
}
