using System.Net;
using FluentAssertions;
using logindirector.Models.AdaptorService;
using logindirector.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;

namespace LoginDirectorTests.Services
{
    [TestFixture]
    public class AdaptorClientServicesTests
    {
        private IConfiguration _configuration;
        private Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private HttpClient _httpClient;
        private AdaptorClientServices _adaptorService;

        [SetUp]
        public void SetUp()
        {
            Dictionary<string, string?> inMemorySettings = new Dictionary<string, string?>
            {
                { "SsoService:SsoDomain", "https://sso.example.com" },
                { "SsoService:RoutePaths:AdaptorPath", "/api/v1/adaptor" },
                { "SsoService:AdaptorKey", "test-adaptor-api-key" },
                { "SsoService:ClientId", "test-client-id" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            _adaptorService = new AdaptorClientServices(_httpClient, _configuration);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
        }

        #region GetUserInformation Tests

        [Test]
        public async Task GetUserInformation_WhenValidUser_ReturnsDeserializedUserAndSendsCorrectHeaders()
        {
            // Arrange
            string username = "user@example.com";
            AdaptorUserModel expectedUser = new AdaptorUserModel
            {
                emailAddress = username,
                coreRoles =
                [
                    new AdaptorUserRoleModel { roleKey = "CAT_USER" }
                ]
            };
            string jsonResponse = JsonConvert.SerializeObject(expectedUser);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    // Assert URL construction, query params, and API headers
                    request.RequestUri!.ToString().Should().Be("https://sso.example.com/api/v1/adaptor?user-name=user%40example.com");
                    request.Headers.Accept.ToString().Should().Contain("application/json");
                    request.Headers.GetValues("X-API-Key").Should().Contain("test-adaptor-api-key");
                    request.Headers.GetValues("X-Consumer-ClientId").Should().Contain("test-client-id");

                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(jsonResponse)
                    };
                });

            // Act
            AdaptorUserModel result = await _adaptorService.GetUserInformation(username);

            // Assert
            result.Should().NotBeNull();
            result.emailAddress.Should().Be(username);
            result.coreRoles.Should().HaveCount(1);
            result.coreRoles[0].roleKey.Should().Be("CAT_USER");
        }

        [Test]
        public async Task GetUserInformation_WhenUsernameHasSpecialChars_EncodesUrlQuery()
        {
            // Arrange
            string username = "user+test#1@example.com";
            AdaptorUserModel expectedUser = new AdaptorUserModel { emailAddress = username };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                {
                    request.RequestUri!.Query.Should().Be("?user-name=user%2btest%231%40example.com");

                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonConvert.SerializeObject(expectedUser))
                    };
                });

            // Act
            AdaptorUserModel result = await _adaptorService.GetUserInformation(username);

            // Assert
            result.Should().NotBeNull();
            result.emailAddress.Should().Be(username);
        }

        [Test]
        public async Task GetUserInformation_WhenJsonIsInvalid_HandlesExceptionAndReturnsEmptyModel()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{ malformed-json-payload }")
                });

            // Act
            AdaptorUserModel result = await _adaptorService.GetUserInformation("user@example.com");

            // Assert
            result.Should().NotBeNull();
            result.emailAddress.Should().BeNull();
        }

        [Test]
        public async Task GetUserInformation_WhenPerformAdaptorRequestReturnsEmptyString_ReturnsEmptyModel()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });

            // Act
            AdaptorUserModel result = await _adaptorService.GetUserInformation("user@example.com");

            // Assert
            result.Should().NotBeNull();
            result.emailAddress.Should().BeNull();
        }

        #endregion

        #region PerformAdaptorRequest Tests

        [Test]
        public async Task PerformAdaptorRequest_WhenSuccessfulStatusCode_ReturnsResponseContent()
        {
            // Arrange
            string routeUri = "https://sso.example.com/api/v1/adaptor?user-name=test";
            string expectedContent = "{\"emailAddress\":\"test@example.com\"}";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(expectedContent)
                });

            // Act
            string result = await _adaptorService.PerformAdaptorRequest(routeUri);

            // Assert
            result.Should().Be(expectedContent);
        }

        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.Unauthorized)]
        public async Task PerformAdaptorRequest_WhenHttpErrorStatus_CatchesExceptionAndReturnsEmptyString(HttpStatusCode statusCode)
        {
            // Arrange
            string routeUri = "https://sso.example.com/api/v1/adaptor?user-name=test";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode
                });

            // Act
            string result = await _adaptorService.PerformAdaptorRequest(routeUri);

            // Assert
            result.Should().Be(string.Empty);
        }

        [Test]
        public async Task PerformAdaptorRequest_WhenHttpRequestExceptionThrown_CatchesAndReturnsEmptyString()
        {
            // Arrange
            string routeUri = "https://sso.example.com/api/v1/adaptor?user-name=test";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Network failure"));

            // Act
            string result = await _adaptorService.PerformAdaptorRequest(routeUri);

            // Assert
            result.Should().Be(string.Empty);
        }

        #endregion
    }
}