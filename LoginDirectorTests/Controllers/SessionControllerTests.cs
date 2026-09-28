using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using logindirector.Constants;
using logindirector.Controllers;
using logindirector.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LoginDirectorTests.Controllers
{
    [TestFixture]
    public class SessionControllerTests
    {
        private IConfiguration _configuration;
        private IMemoryCache _memoryCache;
        private DefaultHttpContext _httpContext;
        private SessionController _controller;

        [SetUp]
        public void SetUp()
        {
            _httpContext = new DefaultHttpContext();

            // In-Memory IConfiguration Setup
            Dictionary<string, string> inMemorySettings = new Dictionary<string, string>
            {
                { "SsoService:ClientId", "test-client-id" },
                { "SsoService:SsoDomain", "https://sso.example.com" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            // Real MemoryCache instance for accurate cache operations
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            _controller = new SessionController(_configuration, _memoryCache)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = _httpContext
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            _memoryCache.Dispose();
            _controller.Dispose();
            _controller = null!;
        }

        #region Helper Methods

        private void AuthenticateUserWithClaim(string claimType, string value)
        {
            Claim[] claims = [new Claim(claimType, value)];
            ClaimsIdentity identity = new ClaimsIdentity(claims, "TestAuth");
            _httpContext.User = new ClaimsPrincipal(identity);
        }

        private string GenerateJwtTokenWithClaim(string claimType, string claimValue)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            ClaimsIdentity identity = new ClaimsIdentity([new Claim(claimType, claimValue)]);

            SecurityToken token = tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Subject = identity,
                Expires = DateTime.UtcNow.AddHours(1)
            });

            return tokenHandler.WriteToken(token);
        }

        #endregion

        #region BackchannelRpIframe Tests

        [Test]
        public void BackchannelRpIframe_WhenUserHasHashClaim_PopulatesModelAndReturnsPartialView()
        {
            // Arrange
            _httpContext.Request.Host = new HostString("example.com");
            AuthenticateUserWithClaim(ClaimTypes.Hash, "test-hash-session-state");

            // Act
            IActionResult result = _controller.BackchannelRpIframe();

            // Assert
            result.Should().BeOfType<PartialViewResult>();
            PartialViewResult partialResult = (PartialViewResult)result;
            partialResult.ViewName.Should().Be("~/Views/Backchannel/RpIframe.cshtml");

            partialResult.Model.Should().BeOfType<BackchannelModel>();
            BackchannelModel model = (BackchannelModel)partialResult.Model;
            model.ClientId.Should().Be("test-client-id");
            model.RedirectUrl.Should().Be("example.com/director/process-user");
            model.SecurityApiUrl.Should().Be("https://sso.example.com");
            model.SessionState.Should().Be("test-hash-session-state");
        }

        [Test]
        public void BackchannelRpIframe_WhenUserUnauthenticated_ReturnsModelWithNullSessionState()
        {
            // Arrange
            _httpContext.Request.Host = new HostString("example.com");
            _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            IActionResult result = _controller.BackchannelRpIframe();

            // Assert
            result.Should().BeOfType<PartialViewResult>();
            PartialViewResult partialResult = (PartialViewResult)result;
            partialResult.Model.Should().BeOfType<BackchannelModel>();
            BackchannelModel model = (BackchannelModel)partialResult.Model;
            model.SessionState.Should().BeNull();
        }

        #endregion

        #region BackchannelLogout Tests

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BackchannelLogout_WhenTokenNullOrEmpty_Returns400BadRequest(string? invalidToken)
        {
            // Act
            IActionResult result = _controller.BackchannelLogout(invalidToken);

            // Assert
            result.Should().BeOfType<StatusCodeResult>();
            StatusCodeResult statusResult = (StatusCodeResult)result;
            statusResult.StatusCode.Should().Be(400);
        }

        [Test]
        public void BackchannelLogout_WhenTokenMalformed_Returns400BadRequest()
        {
            // Act
            IActionResult result = _controller.BackchannelLogout("not-a-valid-jwt-token");

            // Assert
            result.Should().BeOfType<StatusCodeResult>();
            StatusCodeResult statusResult = (StatusCodeResult)result;
            statusResult.StatusCode.Should().Be(400);
        }

        [Test]
        public void BackchannelLogout_WhenJwtHasNoSidClaim_Returns400BadRequest()
        {
            // Arrange
            string token = GenerateJwtTokenWithClaim("sub", "user-123");

            // Act
            IActionResult result = _controller.BackchannelLogout(token);

            // Assert
            result.Should().BeOfType<StatusCodeResult>();
            StatusCodeResult statusResult = (StatusCodeResult)result;
            statusResult.StatusCode.Should().Be(400);
        }

        [Test]
        public void BackchannelLogout_WhenValidJwtWithSidClaim_RemovesSessionAndReturns200OK()
        {
            // Arrange
            string targetSessionId = "session-to-remove";
            string token = GenerateJwtTokenWithClaim("sid", targetSessionId);

            List<UserSessionModel> initialCache =
            [
                new UserSessionModel { sessionId = targetSessionId },
                new UserSessionModel { sessionId = "session-to-keep" }
            ];
            _memoryCache.Set(AppConstants.CentralCache_Key, initialCache);

            // Act
            IActionResult result = _controller.BackchannelLogout(token);

            // Assert
            result.Should().BeOfType<StatusCodeResult>();
            StatusCodeResult statusResult = (StatusCodeResult)result;
            statusResult.StatusCode.Should().Be(200);

            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cachedSessions).Should().BeTrue();
            cachedSessions.Should().HaveCount(1);
            cachedSessions.Should().NotContain(s => s.sessionId == targetSessionId);
        }

        #endregion

        #region RemoveUserFromCentralSessionCache Tests

        [Test]
        public void RemoveUserFromCentralSessionCache_WhenCacheEmpty_SetsNullListInCache()
        {
            // Act
            _controller.RemoveUserFromCentralSessionCache("session-123");

            // Assert
            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cachedSessions).Should().BeTrue();
            cachedSessions.Should().BeNull();
        }

        [Test]
        public void RemoveUserFromCentralSessionCache_WhenTargetSessionExists_FiltersItOut()
        {
            // Arrange
            List<UserSessionModel> sessions =
            [
                new UserSessionModel { sessionId = "session-1" },
                new UserSessionModel { sessionId = "session-2" },
                new UserSessionModel { sessionId = "session-1" }
            ];
            _memoryCache.Set(AppConstants.CentralCache_Key, sessions);

            // Act
            _controller.RemoveUserFromCentralSessionCache("session-1");

            // Assert
            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cachedSessions).Should().BeTrue();
            cachedSessions.Should().HaveCount(1);
            cachedSessions[0].sessionId.Should().Be("session-2");
        }

        [Test]
        public void RemoveUserFromCentralSessionCache_WhenTargetSessionDoesNotExist_LeavesCacheUnchanged()
        {
            // Arrange
            List<UserSessionModel> sessions =
            [
                new UserSessionModel { sessionId = "session-1" }
            ];
            _memoryCache.Set(AppConstants.CentralCache_Key, sessions);

            // Act
            _controller.RemoveUserFromCentralSessionCache("non-existent-session");

            // Assert
            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cachedSessions).Should().BeTrue();
            cachedSessions.Should().HaveCount(1);
            cachedSessions[0].sessionId.Should().Be("session-1");
        }

        #endregion
    }
}