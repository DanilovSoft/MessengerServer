using System;
using System.Threading;
using wRPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using MessengerServer.Extensions;
using Microsoft.Extensions.Logging;

namespace MessengerServer
{
    class Program
    {
        private const int Port = 65125;

        static void Main()
        {
            Console.Title = "Сервер";
            using (var mutex = new Mutex(initiallyOwned: true, $"MessengerServer_Port:{Port}", out bool createdNew))
            {
                if (createdNew)
                {
                    IConfiguration configuration = BuildConfiguration();

                    using (var listener = new Listener(Port))
                    {
                        listener.IoC.AddDb(configuration);

                        listener.IoC.AddLogging(loggingBuilder =>
                        {
                            loggingBuilder
                                .AddConsole()
                                .AddDebug();
                        });

                        listener.StartAccept();
                        ILogger logger = listener.ServiceProvider.GetRequiredService<ILogger<Program>>();
                        logger.LogInformation("Ожидание подключений...");

                        Thread.Sleep(-1);
                    }
                }
            }
        }

        private static IConfiguration BuildConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true);

            return configurationBuilder.Build();
        }
    }
}
