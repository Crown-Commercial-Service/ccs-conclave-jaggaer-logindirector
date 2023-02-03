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
using logindirector.Models.AdaptorService;
using Newtonsoft.Json;

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
        public async Task<UserStatusModel> GetUserStatus(string username, string accessToken, AdaptorUserModel userModel, bool isPostProcessing)
        {
            UserStatusModel model = null;

            try
            {
                // Fetch the information we need from the User GET route
                string userRouteUri = _configuration.GetValue<string>("TendersApi:ApiDomain") + _configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.HtmlEncode(username), accessToken, HttpMethod.Get);

                if (responseModel != null)
                {
                    // We now need to process the response according to whether this request is pre or post user processing
                    if (!isPostProcessing)
                    {
                        // This is pre user processing
                        model = HandleUserStatusResponsePreProcessing(responseModel);
                    }
                    else
                    {
                        // This is post user processing
                        model = HandleUserStatusResponsePostProcessing(responseModel, userModel);
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

        // Processes a user response from Tenders GetUserStatus according to the rules in place for when querying before the user has been processed
        internal UserStatusModel HandleUserStatusResponsePreProcessing(GenericResponseModel responseModel)
        {
            UserStatusModel model = new UserStatusModel();

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
                // The user exists in Jaegger and their account has already been merged
                model.UserStatus = AppConstants.Tenders_UserStatus_AlreadyMerged;
            }
            else
            {
                // This is an unexpected error response from Tenders that we can't handle
                RollbarLocator.RollbarInstance.Info("Invalid Tenders Response - StatusCode: " + responseModel.StatusCode + " --- ResponseValue: " + responseModel.ResponseValue);

                model.UserStatus = AppConstants.Tenders_UserStatus_Error;
            }

            return model;
        }

        // Processes a user response from Tenders GetUserStatus according to the rules in place for when querying after the user has been processed
        internal UserStatusModel HandleUserStatusResponsePostProcessing(GenericResponseModel responseModel, AdaptorUserModel userModel)
        {
            UserStatusModel model = new UserStatusModel();

            // First we need to map the roles detail in the response to a usable model
            RolesResponseModel rolesResponseModel = new RolesResponseModel();

            if (!String.IsNullOrWhiteSpace(responseModel.ResponseValue) && responseModel.ResponseValue.Contains("roles"))
            {
                rolesResponseModel = JsonConvert.DeserializeObject<RolesResponseModel>(responseModel.ResponseValue);
            }

            // Now we should have all the information we need to determine user state
            if (userModel != null)
            {
                if (responseModel.StatusCode == HttpStatusCode.OK && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && rolesResponseModel.roles.Count == 1 && rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer))
                {
                    // The user has been successfully merged and the roles Tenders reports matches with the roles PPG reports (AC2)
                    model.UserStatus = AppConstants.Tenders_UserStatus_AlreadyMerged;
                }
                else if (responseModel.StatusCode == HttpStatusCode.Conflict || (userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Supplier)) || (userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerSupplier) && rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer)))
                {
                    // Looks like there's a role mismatch between the PPG roles and what Tenders has actually created (AC4, AC6)
                    model.UserStatus = AppConstants.Tenders_UserStatus_Conflict;
                }
                else if (responseModel.StatusCode == HttpStatusCode.OK && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && !rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer))
                {
                    // Looks like the merge failed at the Tenders end (AC3)
                    model.UserStatus = AppConstants.Tenders_UserStatus_MergeFailed;
                }

                // TODO: Finish applying new rules
                // Done up to and including AC6.  AC5 not done, comment added to case (unclear how it's achieved)
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
