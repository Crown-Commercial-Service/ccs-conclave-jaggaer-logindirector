using System.Security.Claims;
using FluentAssertions;
using logindirector.Constants;
using logindirector.Controllers;
using logindirector.Helpers;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using logindirector.Models.TendersApi;
using logindirector.Services;
using LoginDirectorTests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json;

namespace LoginDirectorTests.Controllers
{
    [TestFixture]
    public class UserProcessingControllerTests
    {
        private Mock<IAdaptorClientServices> _mockAdaptorServices;
        private Mock<ITendersClientServices> _mockTendersServices;
        private Mock<IHelpers> _mockUserHelpers;
        private IMemoryCache _memoryCache;
        private IConfiguration _configuration;
        private DefaultHttpContext _httpContext;
        private TestSession _session;

        private UserProcessingController _controller;

        [SetUp]
        public void SetUp()
        {
            _mockAdaptorServices = new Mock<IAdaptorClientServices>();
            _mockTendersServices = new Mock<ITendersClientServices>();
            _mockUserHelpers = new Mock<IHelpers>();

            _httpContext = new DefaultHttpContext();
            _session = new TestSession();
            _httpContext.Session = _session;

            // In-Memory IConfiguration Setup
            Dictionary<string, string?> inMemorySettings = new Dictionary<string, string?>
            {
                { "HandbackPath", "/handback" },
                { "ExternalAuthenticationPath", "https://auth.example.com?redirect=" },
                { "ExitDomains:JaeggerDomain", "exit.jaegger.com" },
                { "ExitDomains:CatDomain", "exit.cat.com" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            _controller = new UserProcessingController(
                _mockAdaptorServices.Object,
                _mockTendersServices.Object,
                _mockUserHelpers.Object,
                _memoryCache,
                _configuration
            )
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

        private void AuthenticateUser(string email = "test@example.com", string sid = "sid-123", string auth = "access-token")
        {
            List<Claim> claims = new List<Claim>();
            if (!string.IsNullOrEmpty(email)) claims.Add(new Claim(ClaimTypes.Email, email));
            if (!string.IsNullOrEmpty(sid)) claims.Add(new Claim(ClaimTypes.Sid, sid));
            if (!string.IsNullOrEmpty(auth)) claims.Add(new Claim(ClaimTypes.Authentication, auth));

            ClaimsIdentity identity = new ClaimsIdentity(claims, "TestAuth");
            _httpContext.User = new ClaimsPrincipal(identity);
        }

        private void SetupValidRequestDetailsInSession(string domain = "exit.cat.com", string path = "/test/path")
        {
            RequestSessionModel requestModel = new RequestSessionModel
            {
                domain = domain,
                requestedPath = path,
                protocol = "https",
                httpFormat = "GET"
            };
            _session.SetString(AppConstants.Session_RequestDetailsKey, JsonConvert.SerializeObject(requestModel));
        }

        #endregion

        #region ProcessUserAsync Tests

        [Test]
        public async Task ProcessUserAsync_WhenPreAuthenticatedUserHasInvalidSession_ReturnsSessionExpiredView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "invalid-sid");
            SetupValidRequestDetailsInSession();
            _session.SetString(AppConstants.Session_UserPreAuthenticated, "true");

            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "invalid-sid"))
                            .ReturnsAsync(false);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/SessionExpired.cshtml");
            viewResult.Model.Should().Be(errorViewModel);
        }

        [Test]
        public async Task ProcessUserAsync_WhenRequestDetailsMissingInSession_ReturnsSessionExpiredView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid");
            // Session request details NOT set

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/SessionExpired.cshtml");
        }

        [Test]
        public async Task ProcessUserAsync_WhenUserHasInvalidRoles_ReturnsUnauthorisedView()
        {
            // Arrange
            AuthenticateUser("user@test.com");
            SetupValidRequestDetailsInSession();

            AdaptorUserModel userModel = new AdaptorUserModel { emailAddress = "user@test.com" };
            _mockAdaptorServices.Setup(s => s.GetUserInformation("user@test.com"))
                                .ReturnsAsync(userModel);

            _mockUserHelpers.Setup(h => h.HasValidUserRoles(userModel, It.IsAny<RequestSessionModel>()))
                            .Returns(false);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Unauthorised.cshtml");
        }

        [Test]
        public async Task ProcessUserAsync_WhenAdaptorReturnsNull_ReturnsUnauthorisedView()
        {
            // Arrange
            AuthenticateUser("user@test.com");
            SetupValidRequestDetailsInSession();

            _mockAdaptorServices.Setup(s => s.GetUserInformation("user@test.com"))
                                .ReturnsAsync((AdaptorUserModel?)null);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Unauthorised.cshtml");
        }

        [Test]
        public async Task ProcessUserAsync_WhenTendersReturnsActionRequired_ReturnsMergePromptView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "sid-123", "token-xyz");
            SetupValidRequestDetailsInSession("exit.jaegger.com");

            AdaptorUserModel userModel = new AdaptorUserModel { emailAddress = "user@test.com" };
            _mockAdaptorServices.Setup(s => s.GetUserInformation("user@test.com")).ReturnsAsync(userModel);
            _mockUserHelpers.Setup(h => h.HasValidUserRoles(userModel, It.IsAny<RequestSessionModel>())).Returns(true);

            UserStatusModel statusModel = new UserStatusModel { UserStatus = AppConstants.Tenders_UserStatus_ActionRequired };
            _mockTendersServices.Setup(t => t.GetUserStatus("user@test.com", "token-xyz")).ReturnsAsync(statusModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Merging/MergePrompt.cshtml");

            viewResult.Model.Should().BeOfType<ServiceViewModel>();
            ServiceViewModel model = (ServiceViewModel)viewResult.Model;
            model.ServiceDisplayName.Should().Be(AppConstants.Display_JaeggerServiceName);
        }

        [Test]
        public async Task ProcessUserAsync_WhenTendersReturnsAlreadyMerged_RedirectsToActionRequest()
        {
            // Arrange
            AuthenticateUser("user@test.com", "sid-123", "token-xyz");
            SetupValidRequestDetailsInSession();

            AdaptorUserModel userModel = new AdaptorUserModel { emailAddress = "user@test.com" };
            _mockAdaptorServices.Setup(s => s.GetUserInformation("user@test.com")).ReturnsAsync(userModel);
            _mockUserHelpers.Setup(h => h.HasValidUserRoles(userModel, It.IsAny<RequestSessionModel>())).Returns(true);

            UserStatusModel statusModel = new UserStatusModel { UserStatus = AppConstants.Tenders_UserStatus_AlreadyMerged };
            _mockTendersServices.Setup(t => t.GetUserStatus("user@test.com", "token-xyz")).ReturnsAsync(statusModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ActionRequest");
            redirect.ControllerName.Should().Be("Request");
        }

        [TestCase(AppConstants.Tenders_UserStatus_Unauthorised, "~/Views/Errors/Unauthorised.cshtml")]
        [TestCase(AppConstants.Tenders_UserStatus_Conflict, "~/Views/Errors/RoleConflict.cshtml")]
        public async Task ProcessUserAsync_WhenTendersStatusIsError_ReturnsCorrespondingErrorView(string tenderStatus, string expectedView)
        {
            // Arrange
            AuthenticateUser("user@test.com", "sid-123", "token-xyz");
            SetupValidRequestDetailsInSession();

            AdaptorUserModel userModel = new AdaptorUserModel { emailAddress = "user@test.com" };
            _mockAdaptorServices.Setup(s => s.GetUserInformation("user@test.com")).ReturnsAsync(userModel);
            _mockUserHelpers.Setup(h => h.HasValidUserRoles(userModel, It.IsAny<RequestSessionModel>())).Returns(true);

            UserStatusModel statusModel = new UserStatusModel { UserStatus = tenderStatus };
            _mockTendersServices.Setup(t => t.GetUserStatus("user@test.com", "token-xyz")).ReturnsAsync(statusModel);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be(expectedView);
        }

        [Test]
        public async Task ProcessUserAsync_WhenUserHasNoEmailClaim_ReturnsGenericErrorView()
        {
            // Arrange
            AuthenticateUser("", "sid-123", "token-xyz"); // Email claim empty
            SetupValidRequestDetailsInSession();

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/Generic.cshtml");
        }

        #endregion

        #region ProcessUserMergeSelectionAsync Tests

        [Test]
        public async Task ProcessUserMergeSelectionAsync_WhenSessionInvalid_ReturnsSessionExpiredView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "invalid-sid");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "invalid-sid"))
                            .ReturnsAsync(false);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserMergeSelectionAsync("merge");

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/SessionExpired.cshtml");
        }

        [Test]
        public async Task ProcessUserMergeSelectionAsync_WhenAccountDecisionIsMerge_RedirectsToExternalAuth()
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid");
            _httpContext.Request.Host = new HostString("mysite.com");

            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "valid-sid"))
                            .ReturnsAsync(true);

            // Act
            IActionResult result = await _controller.ProcessUserMergeSelectionAsync("merge");

            // Assert
            result.Should().BeOfType<RedirectResult>();
            RedirectResult redirectResult = (RedirectResult)result;
            redirectResult.Url.Should().Be("https://auth.example.com?redirect=https://mysite.com/handback");
        }

        [Test]
        public async Task ProcessUserMergeSelectionAsync_WhenCreateAccountSuccess_RedirectsToActionRequest()
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid", "token-xyz");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "valid-sid")).ReturnsAsync(true);

            UserCreationModel creationModel = new UserCreationModel { CreationStatus = AppConstants.Tenders_UserCreation_Success };
            _mockTendersServices.Setup(t => t.CreateJaeggerUser("user@test.com", "token-xyz"))
                                .ReturnsAsync(creationModel);

            // Act
            IActionResult result = await _controller.ProcessUserMergeSelectionAsync("create");

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ActionRequest");
            redirect.ControllerName.Should().Be("Request");
        }

        [TestCase(AppConstants.Tenders_UserCreation_Conflict, "~/Views/Errors/RoleConflict.cshtml")]
        [TestCase(AppConstants.Tenders_UserCreation_MissingRole, "~/Views/Errors/Unauthorised.cshtml")]
        [TestCase(AppConstants.Tenders_UserCreation_HelpdeskRequired, "~/Views/Errors/BothRolesAssigned.cshtml")]
        [TestCase(AppConstants.Tenders_UserCreation_AlreadyExists, "~/Views/Errors/ExistingAccount.cshtml")]
        public async Task ProcessUserMergeSelectionAsync_WhenCreateAccountReturnsErrorStatus_ReturnsExpectedView(string creationStatus, string expectedViewName)
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid", "token-xyz");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "valid-sid")).ReturnsAsync(true);

            UserCreationModel creationModel = new UserCreationModel { CreationStatus = creationStatus };
            _mockTendersServices.Setup(t => t.CreateJaeggerUser("user@test.com", "token-xyz"))
                                .ReturnsAsync(creationModel);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserMergeSelectionAsync("create");

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be(expectedViewName);
        }

        [Test]
        public async Task ProcessUserMergeSelectionAsync_WhenCreateAccountFailsOrReturnsNull_ReturnsCreateErrorView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid", "token-xyz");
            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "valid-sid")).ReturnsAsync(true);

            _mockTendersServices.Setup(t => t.CreateJaeggerUser("user@test.com", "token-xyz"))
                                .ReturnsAsync((UserCreationModel?)null);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ProcessUserMergeSelectionAsync("create");

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/CreateError.cshtml");
        }

        #endregion

        #region ContinueProcessingMergedUserAsync Tests

        [Test]
        public async Task ContinueProcessingMergedUserAsync_WhenValidSessionAndRequestDetails_RedirectsToActionRequest()
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid", "token-xyz");
            SetupValidRequestDetailsInSession("exit.cat.com", "/dashboard");

            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "valid-sid"))
                            .ReturnsAsync(true);

            // Act
            IActionResult result = await _controller.ContinueProcessingMergedUserAsync();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("ActionRequest");
            redirect.ControllerName.Should().Be("Request");
        }

        [Test]
        public async Task ContinueProcessingMergedUserAsync_WhenSessionInvalid_ReturnsSessionExpiredView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "invalid-sid", "token-xyz");
            SetupValidRequestDetailsInSession();

            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "invalid-sid"))
                            .ReturnsAsync(false);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ContinueProcessingMergedUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/SessionExpired.cshtml");
        }

        [Test]
        public async Task ContinueProcessingMergedUserAsync_WhenRequestDetailsMissing_ReturnsSessionExpiredView()
        {
            // Arrange
            AuthenticateUser("user@test.com", "valid-sid", "token-xyz");
            // No request details stored in session

            _mockUserHelpers.Setup(h => h.DoesUserHaveValidSession(_httpContext, "valid-sid"))
                            .ReturnsAsync(true);

            ErrorViewModel errorViewModel = new ErrorViewModel();
            _mockUserHelpers.Setup(h => h.BuildErrorModelForUser(It.IsAny<string?>())).Returns(errorViewModel);

            // Act
            IActionResult result = await _controller.ContinueProcessingMergedUserAsync();

            // Assert
            result.Should().BeOfType<ViewResult>();
            ViewResult viewResult = (ViewResult)result;
            viewResult.ViewName.Should().Be("~/Views/Errors/SessionExpired.cshtml");
        }

        #endregion

        #region Cache and Internal Helpers Tests

        [Test]
        public void AddUserToCentralSessionCache_WhenNewUser_AddsUserToCache()
        {
            // Arrange
            AuthenticateUser("user@test.com");
            AdaptorUserModel userModel = new AdaptorUserModel { emailAddress = "user@test.com" };

            // Act
            _controller.AddUserToCentralSessionCache(userModel);

            // Assert
            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cacheList).Should().BeTrue();
            cacheList.Should().NotBeNull();
            cacheList.Should().HaveCount(1);
            cacheList[0].userEmail.Should().Be("user@test.com");
            cacheList[0].sessionId.Should().Be("sid-123");
        }

        [Test]
        public void AddUserToCentralSessionCache_WhenUserAlreadyExists_DoesNotAddDuplicate()
        {
            // Arrange
            AuthenticateUser("user@test.com");
            AdaptorUserModel userModel = new AdaptorUserModel { emailAddress = "user@test.com" };

            List<UserSessionModel> existingList =
            [

                new UserSessionModel
                {
                    userEmail = "user@test.com",
                    sessionId = "sid-123",
                    sessionStart = DateTime.Now
                }
            ];
            _memoryCache.Set(AppConstants.CentralCache_Key, existingList);

            // Act
            _controller.AddUserToCentralSessionCache(userModel);

            // Assert
            _memoryCache.TryGetValue(AppConstants.CentralCache_Key, out List<UserSessionModel>? cacheList).Should().BeTrue();
            cacheList.Should().HaveCount(1);
        }

        [Test]
        public void GetServiceViewModelForRequest_WhenJaeggerDomain_ReturnsJaeggerDisplayName()
        {
            // Arrange
            SetupValidRequestDetailsInSession("exit.jaegger.com");

            // Act
            ServiceViewModel result = _controller.GetServiceViewModelForRequest();

            // Assert
            result.ServiceDisplayName.Should().Be(AppConstants.Display_JaeggerServiceName);
        }

        [Test]
        public void GetServiceViewModelForRequest_WhenCatDomain_ReturnsCatDisplayName()
        {
            // Arrange
            SetupValidRequestDetailsInSession();

            // Act
            ServiceViewModel result = _controller.GetServiceViewModelForRequest();

            // Assert
            result.ServiceDisplayName.Should().Be(AppConstants.Display_CatServiceName);
        }

        #endregion
    }
}