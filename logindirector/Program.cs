using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Steeltoe.Common.Hosting;
using System;
using System.Linq;
using Steeltoe.Extensions.Configuration.CloudFoundry;

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

        private static IHostBuilder CreateCloudFoundryHostBuilder(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .UseCloudHosting(5000, 2021)
                .AddCloudFoundryConfiguration()

                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    bool isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == Environments.Development;

                    if (!isDevelopment)
                    {
                        IConfigurationRoot configg = config.Build();
                        CloudFoundryServicesOptions cloudServiceConfig = configg.GetSection("vcap").Get<CloudFoundryServicesOptions>();
                        string cf_aws_access_key_id = cloudServiceConfig.Services["user-provided"].First(s => s.Name == "aws-ssm").Credentials["aws_access_key_id"].Value;
                        string cf_aws_secret_access_key = cloudServiceConfig.Services["user-provided"].First(s => s.Name == "aws-ssm").Credentials["aws_secret_access_key"].Value;
                        string cf_aws_region = cloudServiceConfig.Services["user-provided"].First(s => s.Name == "aws-ssm").Credentials["region"].Value;

                        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", cf_aws_access_key_id);
                        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", cf_aws_secret_access_key);
                        Environment.SetEnvironmentVariable("AWS_REGION", cf_aws_region);
                    }

                    config.AddSystemsManager($"/", TimeSpan.FromMinutes(5));

                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }

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
