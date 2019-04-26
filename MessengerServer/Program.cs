using MessengerServer.Controllers;
using System;
using wRPC;

namespace MessengerServer
{
    class Program
    {
        static void Main()
        {
            using (var listener = new Listener(port: 1234, new AuthController()))
            {
                listener.StartAccept();
                Console.ReadLine();
            }
        }
    }
}
