using System;
using System.Threading;
using vRPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using MessengerServer.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading.Tasks;

namespace MessengerServer
{
    class Program
    {
        private const int Port = 65125;

        static async Task Main()
        {
            Console.Title = "Сервер";
            using (var mutex = new Mutex(initiallyOwned: true, $"MessengerServer_Port:{Port}", out bool createdNew))
            {
                if (createdNew)
                {
                    IConfiguration configuration = BuildConfiguration();

                    using (var listener = new Listener(IPAddress.Any, Port))
                    {
                        listener.ConfigureService(ioc =>
                        {
                            ioc.AddDb(configuration);

                            ioc.AddLogging(loggingBuilder =>
                            {
                                loggingBuilder
                                    .AddConsole()
                                    .AddDebug();
                            });
                        });

                        listener.Configure(serviceProvider => 
                        {
                            ILogger logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                            logger.LogInformation("Ожидание подключений...");
                        });

                        await listener.RunAsync();
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
