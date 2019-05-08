using DbModel.Store;
using EfProvider;
using MessengerServer.Controllers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using wRPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;

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
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                    IConfigurationRoot configuration = configurationBuilder.Build();
                    
                    using (var listener = new Listener(Port))
                    {
                        var modelStore = new ModelStore();
                        var builder = new DbContextOptionsBuilder<CustomEfDbContext>();
                        builder.UseNpgsql(configuration.GetConnectionString("Default"));
                        
                        listener.IoC.AddScoped(x => new CustomEfDbContext(modelStore, builder.Options));
                        listener.IoC.AddScoped<IDataProvider, EfDataProvider>();

                        Console.WriteLine("Ожидание подключений...");
                        listener.StartAccept();

                        Thread.Sleep(-1);
                    }
                }
            }
        }
    }
}
