using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;
using wRPC.Contract;

namespace StubClient.Controllers
{
    internal class HomeController : Controller, IClientController
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
