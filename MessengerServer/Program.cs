using System;
using wRPC;

namespace MessengerServer
{
    class Program
    {
        static void Main()
        {
            using (var listener = new Listener(port: 1234))
            {
                listener.StartAccept();
                Console.ReadLine();
            }
        }
    }
}
