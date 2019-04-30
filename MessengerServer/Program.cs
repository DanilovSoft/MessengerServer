using MessengerServer.Controllers;
using System;
using System.Threading;
using wRPC;

namespace MessengerServer
{
    class Program
    {
        private const int Port = 1234;

        static void Main()
        {
            using (var listener = new Listener(port: 1234))
            {
                using (var mutex = new Mutex(initiallyOwned: true, $"MessengerServer_Port:{Port}", out bool createdNew))
                {
                    if (createdNew)
                    {
                        listener.IOC.Bind<ISqlContext>().To<SqlContext>();

                        listener.StartAccept();
                        while (true)
                        {
                            Console.ReadKey(intercept: true);
                        }
                    }
                    mutex.ReleaseMutex();
                }
            }
        }
    }

    public class SqlContext : ISqlContext
    {

    }

    public interface ISqlContext
    {
    }
}
