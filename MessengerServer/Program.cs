using MessengerServer.Controllers;
using System;
using System.Collections.Generic;
using System.Threading;
using wRPC;

namespace MessengerServer
{
    class Program
    {
        private const int Port = 1234;

        static void Main()
        {
            Console.Title = "Сервер";
            using (var mutex = new Mutex(initiallyOwned: true, $"MessengerServer_Port:{Port}", out bool createdNew))
            {
                if (createdNew)
                {
                    using (var listener = new Listener(Port))
                    {
                        ///listener.IOC.Bind<ISqlContext>().To<SqlContext>();

                        listener.StartAccept();

                        Thread.Sleep(-1);
                    }
                }
            }
        }
    }
}
