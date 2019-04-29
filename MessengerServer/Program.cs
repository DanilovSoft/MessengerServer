using MessengerServer.Controllers;
using System;
using wRPC;

namespace MessengerServer
{
    class Program
    {
        static void Main()
        {
            using (var listener = new Listener(port: 1234, typeof(AuthController)))
            {
                listener.StartAccept();
                Console.ReadLine();
            }
        }
    }
}
