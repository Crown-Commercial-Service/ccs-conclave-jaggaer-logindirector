using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Common.Hosting;
using System.Linq;

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
            .UseCloudHosting(5000, 2021)
            .AddCloudFoundryConfiguration()

                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    string envName = hostingContext.HostingEnvironment.EnvironmentName.ToString().ToLower();
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

                    config.AddSystemsManager($"/{"development"}", TimeSpan.FromMinutes(5));

                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
