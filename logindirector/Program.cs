using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Reflection;
using Steeltoe.Configuration.CloudFoundry;

namespace logindirector
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var deploymentEnvironment = Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT");

            if (string.IsNullOrEmpty(deploymentEnvironment) || deploymentEnvironment == "CloudFoundry")
            {
                CreateCloudFoundryHostBuilder(args).Build().Run();
            }
            else if (deploymentEnvironment == "AWS")
            {
                CreateAWSHostBuilder(args).Build().Run();
            }
        }

        public static IHostBuilder CreateCloudFoundryHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .AddCloudFoundryConfiguration()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    bool isDevelopment = hostingContext.HostingEnvironment.IsDevelopment();
                    bool isTesting = hostingContext.HostingEnvironment.IsEnvironment("Testing") || hostingContext.HostingEnvironment.IsEnvironment("Test");

                    if (!isDevelopment && !isTesting)
                    {
                        IConfigurationRoot interimConfig = config.Build();

                        // "vcap:services" maps directly to VCAP_SERVICES in Steeltoe 4
                        var cloudServiceConfig = interimConfig.GetSection("vcap:services")
                            .Get<CloudFoundryServicesOptions>();

                        var awsSsmService = cloudServiceConfig?.Services?["user-provided"]?
                            .FirstOrDefault(s => string.Equals(s.Name, "aws-ssm", StringComparison.OrdinalIgnoreCase));

                        if (awsSsmService?.Credentials != null)
                        {
                            if (awsSsmService.Credentials.TryGetValue("aws_access_key_id", out var keyId))
                                Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", keyId.Value);

                            if (awsSsmService.Credentials.TryGetValue("aws_secret_access_key", out var secretKey))
                                Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", secretKey.Value);

                            if (awsSsmService.Credentials.TryGetValue("region", out var region))
                                Environment.SetEnvironmentVariable("AWS_REGION", region.Value);
                        }
        
                        // AWS Systems Manager parameter store configuration
                        config.AddSystemsManager("/", TimeSpan.FromMinutes(5));
                    }
                    else if (isDevelopment)
                    {
                        // Make secrets.json optional so CI/CD runners don't crash if it's missing
                        config.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var cfPort = Environment.GetEnvironmentVariable("PORT") ?? Environment.GetEnvironmentVariable("SERVER_PORT");

                    if (!string.IsNullOrEmpty(cfPort))
                    {
                        webBuilder.UseUrls($"http://*:{cfPort}");
                    }
                    else
                    {
                        webBuilder.UseUrls("http://localhost:5000", "https://localhost:2021");
                    }

                    webBuilder.UseStartup<Startup>();
                });

        public static IHostBuilder CreateAWSHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(c =>
                {
                    c.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", true, true)
                        .AddEnvironmentVariables();
                })
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
    }
}