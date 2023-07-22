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
using Newtonsoft.Json;

namespace logindirector.Services
{
    /**
     * Service Client for the Tenders API service - where Jaegger operations are performed against
     */
    public class TendersClientServices : ITendersClientServices
    {
        public IConfiguration _configuration { get; }

        public TendersClientServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /**
         * Retrieves the status of a Jaegger user matching the authenticated user
         */
        public async Task<UserStatusModel> GetUserStatus(string username, string accessToken, string domain)
        {
            UserStatusModel model = null;

            try
            {
                // Fetch the information we need from the User GET route
                string userRouteUri = _configuration.GetValue<string>("TendersApi:ApiDomain") + _configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.UrlEncode(username), accessToken, HttpMethod.Get);

                if (responseModel != null)
                {
                    // We now need to map our response to a useful model to return
                    model = new UserStatusModel();

                    if (responseModel.StatusCode == HttpStatusCode.NotFound && responseModel.ResponseValue.Contains("not found in Jaggaer"))
                    {
                        // The user either doesn't exist in Jaegger, or their account is unmerged
                        model.UserStatus = AppConstants.Tenders_UserStatus_ActionRequired;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // There's either a user mismatch at the Tenders end or the user doesn't have access to the service
                        model.UserStatus = AppConstants.Tenders_UserStatus_Unauthorised;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.Conflict)
                    {
                        // There's a role mismatch between what PPG says the user should be and what Tenders says the user should be
                        model.UserStatus = AppConstants.Tenders_UserStatus_Conflict;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.OK)
                    {
                        // Response suggests the user has already been merged, but we need to validate this
                        model.UserStatus = ValidateTendersResponseMatchesMergedStatus(domain, responseModel);
                    }
                    else
                    {
                        // This is an unexpected error response from Tenders that we can't handle
                        RollbarLocator.RollbarInstance.Info("Invalid Tenders Response - StatusCode: " + responseModel.StatusCode + " --- ResponseValue: " + responseModel.ResponseValue);

                        model.UserStatus = AppConstants.Tenders_UserStatus_Error;
                    }
                }
                else
                {
                    RollbarLocator.RollbarInstance.Error("Tenders API Request returned null response");
                }
            }
            catch (Exception ex)
            {
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return model;
        }

        /**
         * Validates an OK status response from Tenders to be sure that it's really correct when on the CAS domain
         */
        public string ValidateTendersResponseMatchesMergedStatus(string domain, GenericResponseModel responseModel)
        {
            // Status suggests already merged, but we need to check that the response marries up to this
            if (domain == _configuration.GetValue<string>("ExitDomains:CatDomain"))
            {
                ExistingUserRolesModel existingRolesModel = JsonConvert.DeserializeObject<ExistingUserRolesModel>(responseModel.ResponseValue);

                if (existingRolesModel != null && !existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer))
                {
                    // User is on the CAS domain and does not have an existing buyer merged, so despite the 200 this needs flagging to go to the merge prompt
                    return AppConstants.Tenders_UserStatus_ActionRequired;
                }
            }

            // If we've got to here, the user exists in Jaegger and their account has already been merged successfully
            return AppConstants.Tenders_UserStatus_AlreadyMerged;
        }

        /**
         * Requests Jaegger to create a new Jaegger user for the authenticated user
         */
        public async Task<UserCreationModel> CreateJaeggerUser(string username, string accessToken)
        {
            UserCreationModel model = null;

            try
            {
                // Perform our request against the User PUT route
                string userRouteUri = _configuration.GetValue<string>("TendersApi:ApiDomain") + _configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.UrlEncode(username), accessToken, HttpMethod.Put);

                if (responseModel != null)
                {
                    // Now map our response to a model to return
                    model = new UserCreationModel();

                    if (responseModel.StatusCode == HttpStatusCode.OK || responseModel.StatusCode == HttpStatusCode.Created)
                    {
                        // The operation has succeeded and the user was either created or updated
                        model.CreationStatus = AppConstants.Tenders_UserCreation_Success;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // The user doesn't have a buyer or supplier role in PPG
                        model.CreationStatus = AppConstants.Tenders_UserCreation_MissingRole;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.Conflict)
                    {
                        // There's a role mismatch between what PPG says the user should be and what Tenders says the user should be
                        model.CreationStatus = AppConstants.Tenders_UserCreation_Conflict;
                    }
                    else if (responseModel.StatusCode == (HttpStatusCode)418)
                    {
                        // The user has both buyer and supplier roles in PPG - helpdesk intervention required to resolve
                        model.CreationStatus = AppConstants.Tenders_UserCreation_HelpdeskRequired;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        // An account already exists for this user within Jaegger
                        model.CreationStatus = AppConstants.Tenders_UserCreation_AlreadyExists;
                    }
                    else
                    {
                        // This is an unexpected general error response from Tenders
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

        /**
         * Core method that performs a request to the Tenders API using parameters passed to it
         */
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
