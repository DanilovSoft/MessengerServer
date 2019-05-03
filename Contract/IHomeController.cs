using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC.Contract
{
    public interface IHomeController
    {
        Task SendMessage(string message, int userId);
    }
}
