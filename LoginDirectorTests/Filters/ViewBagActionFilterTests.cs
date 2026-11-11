using logindirector.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using FluentAssertions;

namespace LoginDirectorTests.Filters
{
    [TestFixture]
    public class ViewBagActionFilterTests
    {
        private IConfiguration _configuration;
        private ViewBagActionFilter _filter;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> inMemorySettings = new Dictionary<string, string?>
            {
                { "SsoService:SsoDomain", "https://sso.example.com" },
                { "SsoService:RoutePaths:BackchannelPath", "/check-session?client=" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _filter = new ViewBagActionFilter(_configuration);
        }

        #region Test Controller Stub

        private class DummyController : Controller
        {
        }

        #endregion

        #region Helper Methods

        private ResultExecutingContext CreateResultExecutingContext(object controllerInstance, string host, string path)
        {
            DefaultHttpContext httpContext = new DefaultHttpContext
            {
                Request =
                {
                    Host = new HostString(host),
                    Path = path
                }
            };

            ActionContext actionContext = new ActionContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData(),
                ActionDescriptor = new ActionDescriptor()
            };

            IList<IFilterMetadata> filters = new List<IFilterMetadata>();
            IActionResult result = new ViewResult();

            return new ResultExecutingContext(
                actionContext,
                filters,
                result,
                controllerInstance
            );
        }

        #endregion

        #region OnResultExecuting Tests

        [Test]
        public void OnResultExecuting_WhenControllerIsBaseController_AddsOpIframeUrlToViewData()
        {
            // Arrange
            DummyController controller = new DummyController();
            ResultExecutingContext context = CreateResultExecutingContext(
                controller,
                host: "app.example.com",
                path: "/director/process-user"
            );

            // Act
            _filter.OnResultExecuting(context);

            // Assert
            controller.ViewData.ContainsKey("OpIframeUrl").Should().BeTrue();
            
            string expectedUrl = "https://sso.example.com/check-session?client=https://app.example.com/director/process-user";
            controller.ViewData["OpIframeUrl"].Should().Be(expectedUrl);
        }

        [Test]
        public void OnResultExecuting_WhenPathIsEmpty_ConstructsUrlCorrectly()
        {
            // Arrange
            DummyController controller = new DummyController();
            ResultExecutingContext context = CreateResultExecutingContext(
                controller,
                host: "localhost",
                path: ""
            );

            // Act
            _filter.OnResultExecuting(context);

            // Assert
            string expectedUrl = "https://sso.example.com/check-session?client=https://localhost";
            controller.ViewData["OpIframeUrl"].Should().Be(expectedUrl);
        }

        [Test]
        public void OnResultExecuting_WhenControllerIsNotBaseController_DoesNotThrowOrModifyViewData()
        {
            // Arrange (Target is a non-Controller object, e.g. a minimal API or PageModel instance)
            object nonControllerInstance = new object();
            ResultExecutingContext context = CreateResultExecutingContext(
                nonControllerInstance,
                host: "app.example.com",
                path: "/test"
            );

            // Act
            Action act = () => _filter.OnResultExecuting(context);

            // Assert
            act.Should().NotThrow();
        }

        #endregion
    }
}