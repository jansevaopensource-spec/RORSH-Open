using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RCS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args);

            // Configure as Windows Service if on Windows
            if (OperatingSystem.IsWindows())
            {
                builder.UseWindowsService();
            }

            builder.ConfigureServices(services =>
            {
                services.AddHostedService<RcsWorker>();
            });

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}
