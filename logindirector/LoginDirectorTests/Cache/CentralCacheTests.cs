using System.Security.Claims;
using logindirector.Constants;
using logindirector.Controllers;
using logindirector.Helpers;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Assert = NUnit.Framework.Assert;

namespace LoginDirectorTests;

[TestFixture]
public class CentralCacheTests
{
    private const string CommonTestEmail = "test@testmail.com";
    private const string CommonSid = "12345678";

    private IMemoryCache _memoryCache = null!;
    private RequestController _requestController = null!;
    private UserHelpers _userHelpers = null!;
    private UserProcessingController _userProcessingController = null!;
    private SessionController _sessionController = null!;

    [SetUp]
    public void SetUp()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        IConfiguration configuration = new ConfigurationBuilder().Build();

        _userHelpers = new UserHelpers(configuration, _memoryCache);

        IHttpContextAccessor contextAccessor = new HttpContextAccessor();

        _requestController = new RequestController(_memoryCache, configuration, _userHelpers, contextAccessor);
        _userProcessingController = new UserProcessingController(null, null, null, _memoryCache, configuration);
        _sessionController = new SessionController(configuration, _memoryCache);
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
        _userProcessingController.Dispose();
        _requestController.Dispose();
        _sessionController.Dispose();
    }

    [Test]
    public async Task DoesUserHaveValidSession_NoUserInSession_ReturnsFalse()
    {
        // Act
        bool isValid = await _userHelpers.DoesUserHaveValidSession(new DefaultHttpContext(), CommonSid);

        // Assert
        Assert.That(isValid, Is.False);
    }

    [TestCase(-2, CommonTestEmail, CommonSid, ExpectedResult = true)] // Active matching session
    [TestCase(-45, CommonTestEmail, CommonSid, ExpectedResult = false)] // Expired session
    [TestCase(-2, "testing@testmail.com", "245582612", ExpectedResult = false)] // Mismatched user/SID
    public async Task<bool> DoesUserHaveValidSession_WithCacheEntry_ReturnsExpectedResult(
        int sessionAgeMinutes, 
        string cacheEmail, 
        string cacheSid)
    {
        // Arrange
        SetupTestClaimsPrincipal(CommonTestEmail);
        SetupTestCacheEntry(sessionAgeMinutes, cacheEmail, cacheSid);

        // Act & Assert
        return await _userHelpers.DoesUserHaveValidSession(_requestController.ControllerContext.HttpContext, CommonSid);
    }

    [TestCase(null, 0)]
    [TestCase("", 0)]
    [TestCase(CommonTestEmail, 1)]
    public void AddUserToCentralSessionCache_WhenCalled_UpdatesCacheCorrectly(string? emailAddress, int expectedCount)
    {
        // Arrange
        AdaptorUserModel? userModel = emailAddress != null
            ? new AdaptorUserModel { emailAddress = emailAddress }
            : null;

        // Act
        _userProcessingController.AddUserToCentralSessionCache(userModel);

        // Assert
        bool cacheFound = _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? sessionsList);
        
        Assert.Multiple(() =>
        {
            Assert.That(cacheFound, Is.True, "Cache key was not set.");
            Assert.That(sessionsList, Is.Not.Null);
            Assert.That(sessionsList, Has.Count.EqualTo(expectedCount));
        });
    }

    [Test]
    public async Task DoesUserHaveValidSession_AfterBackchannelLogout_ReturnsFalse()
    {
        // Arrange
        SetupTestClaimsPrincipal(CommonTestEmail);
        SetupTestCacheEntry(-2, CommonTestEmail, CommonSid);

        // Act
        _sessionController.RemoveUserFromCentralSessionCache(CommonSid);
        bool isValid = await _userHelpers.DoesUserHaveValidSession(_requestController.ControllerContext.HttpContext, CommonSid);

        // Assert
        Assert.That(isValid, Is.False);
    }

    #region Helper Methods

    private void SetupTestClaimsPrincipal(string emailAddress)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, emailAddress),
            new(ClaimTypes.Sid, CommonSid)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _requestController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private void SetupTestCacheEntry(int sessionAgeMinutes, string emailAddress, string sessionId)
    {
        var userEntry = new UserSessionModel
        {
            userEmail = emailAddress,
            sessionStart = DateTime.Now.AddMinutes(sessionAgeMinutes),
            sessionId = sessionId
        };

        var sessionsList = new List<UserSessionModel> { userEntry };

        _memoryCache.Set(AppConstants.CentralCache_Key, sessionsList);
    }

    #endregion
}