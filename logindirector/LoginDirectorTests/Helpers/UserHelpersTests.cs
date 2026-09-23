using logindirector.Constants;
using logindirector.Helpers;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Assert = NUnit.Framework.Assert;

namespace LoginDirectorTests;

[TestFixture]
public class UserHelpersTests
{
    private const string JaeggerTestDomain = "jaeggertest.com";
    private const string CatTestDomain = "cattest.com";

    private UserHelpers _userHelpers = null!;
    private AdaptorUserModel _userModel = null!;
    private RequestSessionModel _requestSessionModel = null!;

    [SetUp]
    public void SetUp()
    {
        // Avoid spinning up full ServiceCollection / ServiceProvider just for IMemoryCache
        IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());

        var testConfiguration = new Dictionary<string, string?>
        {
            ["ExitDomains:CatDomain"] = CatTestDomain,
            ["ExitDomains:JaeggerDomain"] = JaeggerTestDomain
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(testConfiguration)
            .Build();

        _userHelpers = new UserHelpers(configuration, memoryCache);

        _userModel = new AdaptorUserModel
        {
            coreRoles = new List<AdaptorUserRoleModel>(),
            additionalRoles = new List<string>()
        };

        _requestSessionModel = new RequestSessionModel();
    }

    [TestCase(AppConstants.RoleKey_CatUser, CatTestDomain, ExpectedResult = true)]
    [TestCase(AppConstants.RoleKey_CatUser, JaeggerTestDomain, ExpectedResult = false)]
    [TestCase(AppConstants.RoleKey_JaeggerSupplier, JaeggerTestDomain, ExpectedResult = true)]
    [TestCase(AppConstants.RoleKey_JaeggerBuyer, JaeggerTestDomain, ExpectedResult = true)]
    [TestCase(AppConstants.RoleKey_JaeggerSupplier, CatTestDomain, ExpectedResult = false)]
    [TestCase(AppConstants.RoleKey_JaeggerBuyer, CatTestDomain, ExpectedResult = false)]
    public bool HasValidUserRoles_WithSpecificRoleAndDomain_ReturnsExpectedResult(string role, string domain)
    {
        // Arrange
        _userModel.additionalRoles.Add(role);
        _requestSessionModel.domain = domain;

        // Act & Assert
        return _userHelpers.HasValidUserRoles(_userModel, _requestSessionModel);
    }

    [Test]
    public void HasValidUserRoles_UserWithoutRoles_ReturnsFalse()
    {
        // Arrange
        _requestSessionModel.domain = CatTestDomain;

        // Act
        bool result = _userHelpers.HasValidUserRoles(_userModel, _requestSessionModel);

        // Assert (NUnit Constraint-Based Assertions)
        Assert.That(result, Is.False);
    }
}