using System;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Rollbar;
using logindirector.Constants;
using logindirector.Models.TendersApi;

namespace logindirector.Services
{
    // Service Client for the Tenders API service - where Jaegger operations are performed against
    public class TendersClientServices : ITendersClientServices
    {
        public IConfiguration _configuration { get; }

        public TendersClientServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Retrieves the status of a Jaegger user matching the authenticated user
        public async Task<UserStatusModel> GetUserStatus(string username, string accessToken)
        {
            UserStatusModel model = null;

            try
            {
                // Fetch the information we need from the User GET route
                string userRouteUri = _configuration.GetValue<string>("TendersApi:ApiDomain") + _configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.HtmlEncode(username), accessToken, HttpMethod.Get);

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
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return model;
        }

        // Requests Jaegger to create a new Jaegger user for the authenticated user
        public async Task<UserCreationModel> CreateJaeggerUser(string username, string accessToken)
        {
            UserCreationModel model = null;

            try
            {
                // Perform our request against the User PUT route
                string userRouteUri = _configuration.GetValue<string>("TendersApi:ApiDomain") + _configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.HtmlEncode(username), accessToken, HttpMethod.Put);

                if (responseModel != null)
                {
                    // Now map our response to a model to return
                    model = new UserCreationModel();

                    if (responseModel.StatusCode == HttpStatusCode.OK || responseModel.StatusCode == HttpStatusCode.Created)
                    {
                        // The operation has succeeded
                        model.CreationStatus = AppConstants.Tenders_UserCreation_Success;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.Conflict)
                    {
                        // User cannot be created due to issues with the account
                        model.CreationStatus = AppConstants.Tenders_UserCreation_Failure;
                    }
                    else
                    {
                        // There's been a more general issue with the operation (authentication not matching requested user, for example)
                        model.CreationStatus = AppConstants.Tenders_UserCreation_Error;
                    }
                }
            }
            catch (Exception ex)
            {
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return model;
        }

        // Core method that performs a request to the Tenders API using parameters passed to it
        public async Task<GenericResponseModel> PerformTendersRequest(string routeUri, string accessToken, HttpMethod method)
        {
            GenericResponseModel model = null;

            try
            {
                // Establish a request to the specified route
                HttpRequestMessage request = new HttpRequestMessage(method, routeUri);
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
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return model;
        }
    }
}
