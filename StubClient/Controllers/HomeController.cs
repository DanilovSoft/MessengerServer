using Contract;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace StubClient.Controllers
{
    internal class HomeController : ClientController, IClientController
    {
        public HomeController()
        {
            
        }

        public Task OnMessage(string message, int fromUserId)
        {
            return Task.CompletedTask;
        }
    }
}
