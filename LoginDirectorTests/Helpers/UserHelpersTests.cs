using FluentAssertions;
using logindirector.Constants;
using logindirector.Helpers;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using LoginDirectorTests.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;

namespace LoginDirectorTests.Helpers
{
    [TestFixture]
    public class UserHelpersTests
    {
        private IConfiguration _configuration;
        private IMemoryCache _memoryCache;
        private Mock<IAuthenticationService> _mockAuthService;
        private DefaultHttpContext _httpContext;
        private UserHelpers _userHelpers;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> inMemorySettings = new Dictionary<string, string?>
            {
                { "ExitDomains:CatDomain", "exit.cat.com" },
                { "ExitDomains:JaeggerDomain", "exit.jaegger.com" },
                { "DashboardPath", "https://dashboard.conclave.com" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            _mockAuthService = new Mock<IAuthenticationService>();

            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockAuthService.Object);
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            _httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
                Session = new TestSession() // Set Session to prevent NullReferenceException when .Clear() is called
            };

            _userHelpers = new UserHelpers(_configuration, _memoryCache);
        }

        [TearDown]
        public void TearDown()
        {
            _memoryCache.Dispose();
        }

        #region HasValidUserRoles Tests

        [TestCase("exit.cat.com", AppConstants.RoleKey_CatUser, true)]
        [TestCase("exit.jaegger.com", AppConstants.RoleKey_JaeggerBuyer, true)]
        [TestCase("exit.jaegger.com", AppConstants.RoleKey_JaeggerSupplier, true)]
        [TestCase("exit.cat.com", AppConstants.RoleKey_JaeggerBuyer, false)]
        [TestCase("exit.jaegger.com", AppConstants.RoleKey_CatUser, false)]
        [TestCase("unknown.domain.com", AppConstants.RoleKey_CatUser, false)]
        public void HasValidUserRoles_CoreRoles_ValidatesRoleAndDomain(string domain, string roleKey, bool expectedResult)
        {
            // Arrange
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                coreRoles =
                [
                    new AdaptorUserRoleModel { roleKey = roleKey }
                ]
            };

            RequestSessionModel requestSessionModel = new RequestSessionModel
            {
                domain = domain
            };

            // Act
            bool result = _userHelpers.HasValidUserRoles(userModel, requestSessionModel);

            // Assert
            result.Should().Be(expectedResult);
        }

        [TestCase("exit.cat.com", AppConstants.RoleKey_CatUser, true)]
        [TestCase("exit.jaegger.com", AppConstants.RoleKey_JaeggerBuyer, true)]
        [TestCase("exit.jaegger.com", AppConstants.RoleKey_JaeggerSupplier, true)]
        [TestCase("exit.cat.com", AppConstants.RoleKey_JaeggerBuyer, false)]
        [TestCase("exit.jaegger.com", AppConstants.RoleKey_CatUser, false)]
        [TestCase("unknown.domain.com", AppConstants.RoleKey_CatUser, false)]
        public void HasValidUserRoles_AdditionalRoles_ValidatesRoleAndDomain(string domain, string roleKey, bool expectedResult)
        {
            // Arrange
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                additionalRoles = [roleKey]
            };

            RequestSessionModel requestSessionModel = new RequestSessionModel
            {
                domain = domain
            };

            // Act
            bool result = _userHelpers.HasValidUserRoles(userModel, requestSessionModel);

            // Assert
            result.Should().Be(expectedResult);
        }

        [Test]
        public void HasValidUserRoles_WhenRolesNullOrEmpty_ReturnsFalse()
        {
            // Arrange
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                coreRoles = null,
                additionalRoles = new List<string>()
            };

            RequestSessionModel requestSessionModel = new RequestSessionModel
            {
                domain = "exit.cat.com"
            };

            // Act
            bool result = _userHelpers.HasValidUserRoles(userModel, requestSessionModel);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region BuildErrorModelForUser Tests

        [Test]
        public void BuildErrorModelForUser_WhenSessionJsonNullOrEmpty_ReturnsModelWithDashboardUrlOnly()
        {
            // Act
            ErrorViewModel result = _userHelpers.BuildErrorModelForUser(string.Empty);

            // Assert
            result.Should().NotBeNull();
            result.DashboardUrl.Should().Be("https://dashboard.conclave.com");
            result.Service.Should().BeNull();
        }

        [Test]
        public void BuildErrorModelForUser_WhenSessionJsonHasJaeggerDomain_SetsJaeggerServiceDisplayName()
        {
            // Arrange
            RequestSessionModel sessionModel = new RequestSessionModel
            {
                domain = "exit.jaegger.com"
            };
            string sessionJson = JsonConvert.SerializeObject(sessionModel);

            // Act
            ErrorViewModel result = _userHelpers.BuildErrorModelForUser(sessionJson);

            // Assert
            result.DashboardUrl.Should().Be("https://dashboard.conclave.com");
            result.Service.Should().NotBeNull();
            result.Service!.ServiceDisplayName.Should().Be(AppConstants.Display_JaeggerServiceName);
        }

        [Test]
        public void BuildErrorModelForUser_WhenSessionJsonHasCatDomain_SetsCatServiceDisplayName()
        {
            // Arrange
            RequestSessionModel sessionModel = new RequestSessionModel
            {
                domain = "exit.cat.com"
            };
            string sessionJson = JsonConvert.SerializeObject(sessionModel);

            // Act
            ErrorViewModel result = _userHelpers.BuildErrorModelForUser(sessionJson);

            // Assert
            result.DashboardUrl.Should().Be("https://dashboard.conclave.com");
            result.Service.Should().NotBeNull();
            result.Service!.ServiceDisplayName.Should().Be(AppConstants.Display_CatServiceName);
        }

        #endregion

        #region DoesUserHaveValidSession Tests

        [Test]
        public async Task DoesUserHaveValidSession_WhenValidSessionInCache_ReturnsTrue()
        {
            // Arrange
            string targetSid = "valid-sid-123";
            List<UserSessionModel> sessionsList =
            [

                new UserSessionModel
                {
                    sessionId = targetSid,
                    sessionStart = DateTime.Now
                }
            ];

            _memoryCache.Set(AppConstants.CentralCache_Key, sessionsList);

            // Act
            bool result = await _userHelpers.DoesUserHaveValidSession(_httpContext, targetSid);

            // Assert
            result.Should().BeTrue();
            _mockAuthService.Verify(
                a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
                Times.Never
            );
        }

        [Test]
        public async Task DoesUserHaveValidSession_WhenSessionExpired_FiltersOutAndClearsAuth()
        {
            // Arrange
            string targetSid = "expired-sid-123";
            List<UserSessionModel> sessionsList =
            [

                new UserSessionModel
                {
                    sessionId = targetSid,
                    sessionStart = DateTime.Now.AddMinutes(-20) // Expired (> 15 mins)
                }
            ];

            _memoryCache.Set(AppConstants.CentralCache_Key, sessionsList);

            // Act
            bool result = await _userHelpers.DoesUserHaveValidSession(_httpContext, targetSid);

            // Assert
            result.Should().BeFalse();

            // Verify cache updated and expired entry removed
            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cacheContent).Should().BeTrue();
            cacheContent.Should().BeEmpty();

            // Verify sign-out occurred
            _mockAuthService.Verify(
                a => a.SignOutAsync(_httpContext, "CookieAuth", It.IsAny<AuthenticationProperties>()),
                Times.Once
            );
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task DoesUserHaveValidSession_WhenSidNullOrEmpty_SignsOutAndReturnsFalse(string? invalidSid)
        {
            // Act
            bool result = await _userHelpers.DoesUserHaveValidSession(_httpContext, invalidSid!);

            // Assert
            result.Should().BeFalse();
            _mockAuthService.Verify(
                a => a.SignOutAsync(_httpContext, "CookieAuth", It.IsAny<AuthenticationProperties>()),
                Times.Once
            );
        }

        [Test]
        public async Task DoesUserHaveValidSession_WhenSignOutThrowsException_HandlesExceptionAndReturnsFalse()
        {
            // Arrange
            _mockAuthService.Setup(a => a.SignOutAsync(_httpContext, "CookieAuth", It.IsAny<AuthenticationProperties>()))
                            .ThrowsAsync(new Exception("Auth signout error"));

            // Act
            bool result = await _userHelpers.DoesUserHaveValidSession(_httpContext, "non-existent-sid");

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}