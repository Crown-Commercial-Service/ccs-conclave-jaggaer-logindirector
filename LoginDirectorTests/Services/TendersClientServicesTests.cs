using System.Net;
using FluentAssertions;
using logindirector.Constants;
using logindirector.Models.TendersApi;
using logindirector.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;

namespace LoginDirectorTests.Services
{
    [TestFixture]
    public class TendersClientServicesTests
    {
        private IConfiguration _configuration;
        private Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private HttpClient _httpClient;
        private TendersClientServices _tendersService;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> inMemorySettings = new Dictionary<string, string?>
            {
                { "TendersApi:ApiDomain", "https://tenders.example.com" },
                { "TendersApi:RoutePaths:UserPath", "/api/v1/users/" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            _tendersService = new TendersClientServices(_httpClient, _configuration);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
        }

        #region GetUserStatus Tests

        [TestCase(HttpStatusCode.NotFound, "User not found in Jaggaer", AppConstants.Tenders_UserStatus_ActionRequired)]
        [TestCase(HttpStatusCode.Forbidden, "", AppConstants.Tenders_UserStatus_Unauthorised)]
        [TestCase(HttpStatusCode.Conflict, "", AppConstants.Tenders_UserStatus_Conflict)]
        [TestCase(HttpStatusCode.OK, "", AppConstants.Tenders_UserStatus_AlreadyMerged)]
        [TestCase(HttpStatusCode.BadRequest, "Unexpected payload", AppConstants.Tenders_UserStatus_Error)]
        public async Task GetUserStatus_ReturnsExpectedStatusMapping(HttpStatusCode statusCode, string responseContent, string expectedStatus)
        {
            // Arrange
            string username = "user@example.com";
            string accessToken = "test-bearer-token";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    request.Method.Should().Be(HttpMethod.Get);
                    request.RequestUri!.ToString().Should().Be("https://tenders.example.com/api/v1/users/user%40example.com");
                    request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                    request.Headers.Authorization.Parameter.Should().Be("test-bearer-token");

                    return new HttpResponseMessage
                    {
                        StatusCode = statusCode,
                        Content = new StringContent(responseContent)
                    };
                });

            // Act
            UserStatusModel model = await _tendersService.GetUserStatus(username, accessToken);

            // Assert
            model.Should().NotBeNull();
            model.UserStatus.Should().Be(expectedStatus);
        }

        [Test]
        public async Task GetUserStatus_WhenPerformTendersRequestReturnsNull_ReturnsNullModel()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Network failure"));

            // Act
            UserStatusModel model = await _tendersService.GetUserStatus("user@example.com", "token");

            // Assert
            model.Should().BeNull();
        }

        #endregion

        #region CreateJaeggerUser Tests

        [TestCase(HttpStatusCode.OK, AppConstants.Tenders_UserCreation_Success)]
        [TestCase(HttpStatusCode.Created, AppConstants.Tenders_UserCreation_Success)]
        [TestCase(HttpStatusCode.Forbidden, AppConstants.Tenders_UserCreation_MissingRole)]
        [TestCase(HttpStatusCode.Conflict, AppConstants.Tenders_UserCreation_Conflict)]
        [TestCase((HttpStatusCode)418, AppConstants.Tenders_UserCreation_HelpdeskRequired)]
        [TestCase(HttpStatusCode.InternalServerError, AppConstants.Tenders_UserCreation_AlreadyExists)]
        [TestCase(HttpStatusCode.BadRequest, AppConstants.Tenders_UserCreation_Error)]
        public async Task CreateJaeggerUser_ReturnsExpectedCreationStatusMapping(HttpStatusCode statusCode, string expectedStatus)
        {
            // Arrange
            string username = "user@example.com";
            string accessToken = "test-bearer-token";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    request.Method.Should().Be(HttpMethod.Put);
                    request.RequestUri!.ToString().Should().Be("https://tenders.example.com/api/v1/users/user%40example.com");
                    request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                    request.Headers.Authorization.Parameter.Should().Be("test-bearer-token");

                    return new HttpResponseMessage
                    {
                        StatusCode = statusCode,
                        Content = new StringContent("")
                    };
                });

            // Act
            UserCreationModel model = await _tendersService.CreateJaeggerUser(username, accessToken);

            // Assert
            model.Should().NotBeNull();
            model.CreationStatus.Should().Be(expectedStatus);
        }

        [Test]
        public async Task CreateJaeggerUser_WhenNetworkFails_ReturnsNullModel()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            UserCreationModel model = await _tendersService.CreateJaeggerUser("user@example.com", "token");

            // Assert
            model.Should().BeNull();
        }

        #endregion

        #region PerformTendersRequest Tests

        [Test]
        public async Task PerformTendersRequest_WhenSuccess_ReturnsMappedGenericResponseModel()
        {
            // Arrange
            string routeUri = "https://tenders.example.com/api/v1/users/test";
            string accessToken = "bearer-123";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"status\":\"ok\"}")
                });

            // Act
            GenericResponseModel result = await _tendersService.PerformTendersRequest(routeUri, accessToken, HttpMethod.Get);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(HttpStatusCode.OK);
            result.ResponseValue.Should().Be("{\"status\":\"ok\"}");
        }

        [Test]
        public async Task PerformTendersRequest_WhenExceptionOccurs_CatchesAndReturnsNull()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new InvalidOperationException("Fatal error"));

            // Act
            GenericResponseModel result = await _tendersService.PerformTendersRequest("https://example.com", "token", HttpMethod.Get);

            // Assert
            result.Should().BeNull();
        }

        #endregion
    }
}