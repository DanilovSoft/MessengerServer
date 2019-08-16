using Contract;
using System;
using System.Threading.Tasks;
using vRPC;

namespace StubClient.Controllers
{
    public class HomeController : ClientController
    {
        public HomeController()
        {
            
        }

        public async Task OnMessage(string message, long fromGroupId, Guid messageId)
        {
            Console.WriteLine($"Входное сообщение: {message}");
            await Task.Delay(100);
        }

        public void Typing(long groupId)
        {
            
        }
    }
}
