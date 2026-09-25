using System.Diagnostics;
using System.Security.Claims;
using FluentAssertions;
using logindirector.Constants;
using logindirector.Controllers;
using logindirector.Helpers;
using logindirector.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json;

namespace LoginDirectorTests.Controllers
{
    [TestFixture]
    public class RequestControllerTests
    {
        private Mock<IMemoryCache> _mockMemoryCache;
        private IConfiguration _configuration;
        private Mock<IHelpers> _mockUserHelpers;
        private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private DefaultHttpContext _httpContext;
        private TestSession _session;

        private RequestController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockUserHelpers = new Mock<IHelpers>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            _httpContext = new DefaultHttpContext();
            _session = new TestSession();
            _httpContext.Session = _session;

            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(_httpContext);

            // Default Configuration setup
            Dictionary<string, string> inMemorySettings = new Dictionary<string, string>
            {
                { "SupportedSources:JaeggerSource", "jaegger.example.com" },
                { "SupportedSources:CatSource", "cat.example.com" },
                { "ExitDomains:JaeggerDomain", "exit.jaegger.com" },
                { "ExitDomains:CatDomain", "exit.cat.com" },
                { "DashboardPath", "https://dashboard.conclave.com" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            _controller = new RequestController(
                _mockMemoryCache.Object,
                _configuration,
                _mockUserHelpers.Object,
                _mockHttpContextAccessor.Object
            )
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = _httpContext
                }
            };

            Environment.SetEnvironmentVariable("IsLocal", "false");
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("IsLocal", null);
            _controller.Dispose();
            _controller = null!;
        }

        #region Helper Methods Setup
        
        private void ConfigureRequest(string scheme, string host, string path, string method)
        {
            _httpContext.Request.Scheme = scheme;
            _httpContext.Request.Host = new HostString(host);
            _httpContext.Request.Path = path;
            _httpContext.Request.Method = method;
        }

        private void AuthenticateUser(bool isAuthenticated, string sid = "user-sid-123")
        {
            if (isAuthenticated)
            {
                Claim[] claims = [new Claim(ClaimTypes.Sid, sid)];
                ClaimsIdentity identity = new ClaimsIdentity(claims, "TestAuth");
                _httpContext.User = new ClaimsPrincipal(identity);
            }
            else
            {
                _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }

        #endregion

        #region Index Action Tests

        [Test]
        public void Index_WhenUserFromUnsupportedSource_RedirectsToDashboard()
        {
            // Arrange
            ConfigureRequest("https", "unsupported.domain.com", "/test", "GET");

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectResult>();
            RedirectResult redirectResult = (RedirectResult)result;
            redirectResult.Url.Should().Be("https://dashboard.conclave.com");
        }

        [Test]
        public void Index_WhenGetRequestModelFails_ReturnsGenericErrorView()
        {
            // Arrange
            // Host exists and is supported, but scheme/path/method aren't configured -> getRequestModel returns null
            _httpContext.Request.Host = new HostString("cat.example.com");
            
            ErrorViewModel expectedError = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string>())).Returns(expectedError);

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Generic.cshtml");
            viewResult.Model.Should().Be(expectedError);
        }

        [Test]
        public void Index_WhenPostRequest_JaeggerDomain_RedirectsPreserveMethodToCoreDomain()
        {
            // Arrange
            ConfigureRequest("https", "jaegger.example.com", "/some/path", "POST");

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectResult>();
            RedirectResult redirectResult = (RedirectResult)result;
            redirectResult.Permanent.Should().BeFalse();
            redirectResult.Url.Should().Be("https://exit.jaegger.com");
            redirectResult.PreserveMethod.Should().BeTrue();
        }

        [Test]
        public void Index_WhenPostRequest_CatDomain_RedirectsPreserveMethodToEndpoint()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/some/path", "POST");

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectResult>();
            RedirectResult redirectResult = (RedirectResult)result;
            redirectResult.Url.Should().Be("https://exit.cat.com/some/path");
            redirectResult.PreserveMethod.Should().BeTrue();
        }

        [Test]
        public void Index_WhenGetRequest_ProcessingRequiredIsTrue_RedirectsToProcessUser()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/some/path", "GET");
            _session.SetString(AppConstants.Session_UserKey, "userDataJson");
            _session.SetString(AppConstants.Session_ProcessingRequiredKey, "true");

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ProcessUser");
            redirect.ControllerName.Should().Be("UserProcessing");
        }

        [Test]
        public void Index_WhenGetRequest_ProcessingRequiredIsFalse_RedirectsToActionRequest()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/some/path", "GET");
            _session.SetString(AppConstants.Session_UserKey, "userDataJson");
            _session.SetString(AppConstants.Session_ProcessingRequiredKey, "false");

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ActionRequest");
            redirect.ControllerName.Should().Be("Request");
        }

        [Test]
        public void Index_WhenGetRequest_NoSessionData_RedirectsToProcessUser()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/some/path", "GET");

            // Act
            IActionResult result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ProcessUser");
            redirect.ControllerName.Should().Be("UserProcessing");
        }

        [Test]
        public void Index_WhenUserIsAuthenticated_SetsPreAuthenticatedSessionFlag()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/some/path", "GET");
            AuthenticateUser(true);

            // Act
            _controller.Index();

            // Assert
            _session.GetString(AppConstants.Session_UserPreAuthenticated).Should().Be("true");
        }

        [Test]
        public void Index_WhenUserIsNotAuthenticated_RemovesPreAuthenticatedSessionFlag()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/some/path", "GET");
            AuthenticateUser(false);
            _session.SetString(AppConstants.Session_UserPreAuthenticated, "true");

            // Act
            _controller.Index();

            // Assert
            _session.GetString(AppConstants.Session_UserPreAuthenticated).Should().BeNull();
        }

        #endregion

        #region ActionRequest Tests

        [Test]
        public async Task ActionRequest_WhenSessionIsInvalid_RedirectsToProcessUser()
        {
            // Arrange
            AuthenticateUser(true, "sid-123");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "sid-123"))
                            .ReturnsAsync(false);

            // Act
            IActionResult result = await _controller.ActionRequest();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ProcessUser");
            redirect.ControllerName.Should().Be("UserProcessing");
        }

        [Test]
        public async Task ActionRequest_WhenValidSession_CatDomain_RedirectsToRequestedRoute()
        {
            // Arrange
            AuthenticateUser(true, "sid-123");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "sid-123"))
                            .ReturnsAsync(true);

            RequestSessionModel model = new RequestSessionModel
            {
                protocol = "https",
                domain = "exit.cat.com",
                requestedPath = "/dashboard/view"
            };
            _session.SetString(AppConstants.Session_RequestDetailsKey, JsonConvert.SerializeObject(model));

            // Act
            IActionResult result = await _controller.ActionRequest();

            // Assert
            _session.GetString(AppConstants.Session_ProcessingRequiredKey).Should().Be("false");
            result.Should().BeOfType<RedirectResult>();
            RedirectResult redirect = (RedirectResult)result;
            redirect.Url.Should().Be("https://exit.cat.com/dashboard/view");
        }

        [Test]
        public async Task ActionRequest_WhenValidSession_JaeggerDomain_RedirectsToDomainOnly()
        {
            // Arrange
            AuthenticateUser(true, "sid-123");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "sid-123"))
                            .ReturnsAsync(true);

            RequestSessionModel model = new RequestSessionModel
            {
                protocol = "https",
                domain = "exit.jaegger.com",
                requestedPath = "/dashboard/ignore-this"
            };
            _session.SetString(AppConstants.Session_RequestDetailsKey, JsonConvert.SerializeObject(model));

            // Act
            IActionResult result = await _controller.ActionRequest();

            // Assert
            result.Should().BeOfType<RedirectResult>();
            RedirectResult redirect = (RedirectResult)result;
            redirect.Url.Should().Be("https://exit.jaegger.com");
        }

        [Test]
        public async Task ActionRequest_WhenRequestDetailsMissingInSession_ReturnsSessionExpiredView()
        {
            // Arrange
            AuthenticateUser(true, "sid-123");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "sid-123"))
                            .ReturnsAsync(true);

            ErrorViewModel expectedError = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string>())).Returns(expectedError);

            // Act
            IActionResult result = await _controller.ActionRequest();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/SessionExpired.cshtml");
            viewResult.Model.Should().Be(expectedError);
        }

        #endregion

        #region Internal Methods Tests

        [Test]
        public void IsUserFromSupportedSource_WhenIsLocalTrue_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("IsLocal", "true");

            // Act
            bool result = _controller.isUserFromSupportedSource();

            // Assert
            result.Should().BeTrue();
        }

        [TestCase("jaegger.example.com", true)]
        [TestCase("cat.example.com", true)]
        [TestCase("unsupported.com", false)]
        [TestCase("", false)]
        public void IsUserFromSupportedSource_NonLocal_ValidatesHostName(string host, bool expected)
        {
            // Arrange
            Environment.SetEnvironmentVariable("IsLocal", "false");
            if (!string.IsNullOrEmpty(host))
            {
                _httpContext.Request.Host = new HostString(host);
            }

            // Act
            bool result = _controller.isUserFromSupportedSource();

            // Assert
            result.Should().Be(expected);
        }

        [Test]
        public void StoreRequestDetailsInSession_SerializesAndSavesModel()
        {
            // Arrange
            RequestSessionModel model = new RequestSessionModel { protocol = "https", domain = "test.com", requestedPath = "/path" };

            // Act
            _controller.storeRequestDetailsInSession(model);

            // Assert
            string? json = _session.GetString(AppConstants.Session_RequestDetailsKey);
            json.Should().NotBeNullOrEmpty();
            RequestSessionModel? deserialized = JsonConvert.DeserializeObject<RequestSessionModel>(json);
            Debug.Assert(deserialized != null, nameof(deserialized) + " != null");
            deserialized.domain.Should().Be("test.com");
        }

        [Test]
        public void GetRequestModel_WhenRequestIncomplete_ReturnsNull()
        {
            // Act
            RequestSessionModel model = _controller.getRequestModel();

            // Assert
            model.Should().BeNull();
        }

        [Test]
        public void GetRequestModel_WhenCatSource_SetsCatExitDomain()
        {
            // Arrange
            ConfigureRequest("https", "cat.example.com", "/test", "GET");

            // Act
            RequestSessionModel model = _controller.getRequestModel();

            // Assert
            model.Should().NotBeNull();
            model.protocol.Should().Be("https");
            model.requestedPath.Should().Be("/test");
            model.httpFormat.Should().Be("GET");
            model.domain.Should().Be("exit.cat.com");
        }

        [Test]
        public void GetRequestModel_WhenJaeggerSource_SetsJaeggerExitDomain()
        {
            // Arrange
            ConfigureRequest("https", "jaegger.example.com", "/test", "GET");

            // Act
            RequestSessionModel model = _controller.getRequestModel();

            // Assert
            model.Should().NotBeNull();
            model.domain.Should().Be("exit.jaegger.com");
        }

        #endregion

        #region Error & Unauthorised Actions Tests

        [Test]
        public void Unauthorised_WithExceptionFeature_ReturnsUnauthorisedView()
        {
            // Arrange
            Mock<IExceptionHandlerPathFeature> exceptionFeatureMock = new Mock<IExceptionHandlerPathFeature>();
            exceptionFeatureMock.Setup(f => f.Error).Returns(new Exception("Test Exception"));

            FeatureCollection featureCollection = new FeatureCollection();
            featureCollection.Set(exceptionFeatureMock.Object);
            _mockHttpContextAccessor.Setup(a => a.HttpContext!.Features).Returns(featureCollection);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string>())).Returns(errorViewModel);

            // Act
            IActionResult result = _controller.Unauthorised();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Unauthorised.cshtml");
            viewResult.Model.Should().Be(errorViewModel);
        }

        [Test]
        public void Unauthorised_WithoutExceptionFeature_ReturnsUnauthorisedView()
        {
            // Arrange
            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string>())).Returns(errorViewModel);

            // Act
            IActionResult result = _controller.Unauthorised();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Unauthorised.cshtml");
        }

        [Test]
        public void Error_ReturnsGenericErrorViewWithTraceId()
        {
            // Arrange
            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string>())).Returns(errorViewModel);
            _httpContext.TraceIdentifier = "trace-id-999";

            // Act
            IActionResult result = _controller.Error();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Generic.cshtml");
            
            ErrorViewModel model = viewResult.Model as ErrorViewModel ?? throw new InvalidOperationException();
            model.Should().NotBeNull();
            model.RequestId.Should().Be("trace-id-999");
        }

        #endregion
    }

    /// <summary>
    /// In-memory ISession implementation for controller testing
    /// </summary>
    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public bool IsAvailable => true;
        public string Id => "TestSessionId";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}