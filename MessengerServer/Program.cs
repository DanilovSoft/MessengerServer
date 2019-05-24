using DbModel.Store;
using EfProvider;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using wRPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using DBCore;
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
                    var configurationBuilder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile("appsettings.local.json", optional: true);

                    IConfigurationRoot configuration = configurationBuilder.Build();
                    
                    using (var listener = new Listener(Port))
                    {
                        var modelStore = new ModelStore();
                        var builder = new DbContextOptionsBuilder<CustomEfDbContext>();
                        builder.UseNpgsql(configuration.GetConnectionString("Default"));
                        
                        listener.IoC.AddScoped(x => new CustomEfDbContext(modelStore, builder.Options));
                        listener.IoC.AddScoped<IDataProvider, EfDataProvider>();

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
    }
}
