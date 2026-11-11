using FluentAssertions;
using logindirector;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoginDirectorTests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override IHostBuilder CreateHostBuilder()
        {
            return Program.CreateCloudFoundryHostBuilder([]);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // 1. Set environment to "Testing" so Program.cs doesn't force-load optional:false secrets.json
            builder.UseEnvironment("Testing");

            // 2. Provide dummy configuration settings required for app startup in CI/CD
            builder.ConfigureAppConfiguration((_, config) =>
            {
                Dictionary<string, string?> testSettings = new Dictionary<string, string?>
                {
                    { "SsoService:SsoDomain", "https://sso.example.com" },
                    { "SsoService:RoutePaths:AdaptorPath", "/api/v1/adaptor" },
                    { "SsoService:AdaptorKey", "test-key" },
                    { "SsoService:ClientId", "test-client-id" },
                    { "TendersApi:ApiDomain", "https://tenders.example.com" },
                    { "TendersApi:RoutePaths:UserPath", "/api/v1/users/" },
                    { "DashboardPath", "https://dashboard.example.com" },
                    { "ExitDomains:CatDomain", "exit.cat.com" },
                    { "ExitDomains:JaeggerDomain", "exit.jaegger.com" }
                };

                config.AddInMemoryCollection(testSettings);
            });
        }
    }

    [TestFixture]
    public class AppStartupTests
    {
        private CustomWebApplicationFactory _factory;
        private HttpClient _client;

        [SetUp]
        public void SetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public void Application_ShouldBootstrapAndBuildServicesWithoutExceptions()
        {
            // Act
            using IServiceScope scope = _factory.Services.CreateScope();

            // Assert
            scope.ServiceProvider.Should().NotBeNull();
        }
    }
}