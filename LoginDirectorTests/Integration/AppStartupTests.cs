using logindirector;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace LoginDirectorTests.Integration
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override IHostBuilder CreateHostBuilder()
        {
            // Direct WebApplicationFactory to use your custom CloudFoundry host builder
            return Program.CreateCloudFoundryHostBuilder([]);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
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
            using var scope = _factory.Services.CreateScope();
            scope.ServiceProvider.Should().NotBeNull();
        }
    }
}