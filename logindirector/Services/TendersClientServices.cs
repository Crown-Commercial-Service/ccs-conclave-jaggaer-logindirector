using System;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using logindirector.Constants;
using logindirector.Models.TendersApi;

namespace logindirector.Services
{
    // Service Client for the Tenders API service - where Jaegger operations are performed against
    public class TendersClientServices : ITendersClientServices
    {
        private readonly ILogger<TendersClientServices> _logger;
        public IConfiguration Configuration { get; }

        public TendersClientServices(ILogger<TendersClientServices> logger, IConfiguration configuration)
        {
            _logger = logger;
            Configuration = configuration;
        }

        // Retrieves the status of a Jaegger user matching the authenticated user
        public async Task<UserStatusModel> GetUserStatus(string username, string accessToken)
        {
            UserStatusModel model = null;

            try
            {
                // Fetch the information we need from the User route
                string userRouteUri = Configuration.GetValue<string>("TendersApi:ApiDomain") + Configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.HtmlEncode(username), accessToken);

                if (responseModel != null)
                {
                    // We now need to map our response to a useful model to return
                    model = new UserStatusModel();

                    if (responseModel.StatusCode == HttpStatusCode.NotFound)
                    {
                        // The user either doesn't exist in Jaegger, or their account is unmerged
                        model.UserStatus = AppConstants.Tenders_UserStatus_ActionRequired;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.OK)
                    {
                        // The user exists in Jaegger and their account has already been merged
                        model.UserStatus = AppConstants.Tenders_UserStatus_AlreadyMerged;
                    }
                    else
                    {
                        // This is an unexpected error response from Tenders that we can't handle
                        model.UserStatus = AppConstants.Tenders_UserStatus_Error;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Could not retrieve user information from Tenders API for " + username, ex);
            }

            return model;
        }

        // Core method that performs a request to the Tenders API using parameters passed to it
        public async Task<GenericResponseModel> PerformTendersRequest(string routeUri, string accessToken)
        {
            GenericResponseModel model = null;

            try
            {
                // Establish a GET request to the specified route
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, routeUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                HttpClientHandler handler = new HttpClientHandler();
                using (HttpClient client = new HttpClient(handler))
                {
                    HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    // Now we have a response, we need to map a generic response model from it to use later - because we'll need access to the status code as well as the value later
                    model = new GenericResponseModel
                    {
                        StatusCode = response.StatusCode,
                        ResponseValue = await response.Content.ReadAsStringAsync()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error communicating with Tenders API at " + routeUri, ex);
            }

            return model;
        }
    }
}
