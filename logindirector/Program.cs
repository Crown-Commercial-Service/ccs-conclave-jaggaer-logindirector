using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Steeltoe.Extensions.Configuration.CloudFoundry;

namespace logindirector
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .AddCloudFoundryConfiguration()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    string envName = hostingContext.HostingEnvironment.EnvironmentName.ToString().ToLower();
                    config.AddSystemsManager($"/{envName}", TimeSpan.FromMinutes(5));

                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
