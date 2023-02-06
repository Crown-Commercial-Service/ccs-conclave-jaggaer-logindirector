﻿using System;
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
using logindirector.Models;

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
        public async Task<UserStatusModel> GetUserStatus(string username, string accessToken, AdaptorUserModel userModel, ServiceViewModel serviceModel, bool isPostProcessing)
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
                        model = HandleUserStatusResponsePostProcessing(responseModel, userModel, serviceModel);
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
        internal UserStatusModel HandleUserStatusResponsePostProcessing(GenericResponseModel responseModel, AdaptorUserModel userModel, ServiceViewModel serviceModel)
        {
            UserStatusModel model = new UserStatusModel();

            // First we need to map the roles detail in the response to a usable model
            RolesResponseModel rolesResponseModel = new RolesResponseModel();

            if (!String.IsNullOrWhiteSpace(responseModel.ResponseValue) && responseModel.ResponseValue.Contains("roles"))
            {
                rolesResponseModel = JsonConvert.DeserializeObject<RolesResponseModel>(responseModel.ResponseValue);
            }

            // Now we should have all the information we need to determine user state
            if (userModel != null && serviceModel != null)
            {
                if (serviceModel.ServiceDisplayName == AppConstants.Display_CatServiceName)
                {
                    // We're dealing with a CAS request, so apply CAS validation rules
                    model.UserStatus = GetPostProcessedUserStatusForCAS(responseModel, userModel, rolesResponseModel);
                }
                else
                {
                    // We're dealing with an eSourcing request, so apply eSourcing validation rules
                    model.UserStatus = GetPostProcessedUserStatusForEsourcing(responseModel, userModel, rolesResponseModel);
                }
            }

            return model;
        }

        // Applies user status validation logic for users coming to us on the CAS domain
        internal string GetPostProcessedUserStatusForCAS(GenericResponseModel responseModel, AdaptorUserModel userModel, RolesResponseModel rolesResponseModel)
        {
            // By default, return an error should nothing override it
            string userStatusValue = AppConstants.Tenders_UserStatus_Error;

            if (responseModel.StatusCode == HttpStatusCode.OK && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && rolesResponseModel.roles.Count == 1 && rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer))
            {
                // The user has been successfully merged and the roles Tenders reports matches with the roles PPG reports (AC2, AC7)
                userStatusValue = AppConstants.Tenders_UserStatus_AlreadyMerged;
            }
            else if (userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerSupplier) && !userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer))
            {
                // The user only has a Supplier role in PPG - this isn't valid for the CAS domain (AC6)
                userStatusValue = AppConstants.Tenders_UserStatus_Conflict;
            }
            else if (responseModel.StatusCode == HttpStatusCode.Conflict || (userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Supplier) && !rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer)))
            {
                // Looks like there's a role mismatch between the PPG roles and what Tenders has actually created (AC4)
                userStatusValue = AppConstants.Tenders_UserStatus_MergeMismatch;
            }
            else if (responseModel.StatusCode == HttpStatusCode.OK && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerSupplier) && rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Supplier) && !rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer))
            {
                // Only one of the two accounts the user has has been merged, and it's the supplier.  As this is a CAS domain request this is not enough and more action is required (AC8)
                userStatusValue = AppConstants.Tenders_UserStatus_ActionRequired;
            }
            else if (responseModel.StatusCode == HttpStatusCode.OK && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && !rolesResponseModel.roles.Contains(AppConstants.Tenders_Roles_Buyer))
            {
                // Looks like the merge failed at the Tenders end (AC3)
                userStatusValue = AppConstants.Tenders_UserStatus_MergeFailed;
            }

            // TODO: Currently missing AC5, AC9 and AC10.  Waiting to get response format that can tell me which org an account is in.  Done other than that

            return userStatusValue;
        }

        // Applies user status validation logic for users coming to us on the eSourcing domain
        internal string GetPostProcessedUserStatusForEsourcing(GenericResponseModel responseModel, AdaptorUserModel userModel, RolesResponseModel rolesResponseModel)
        {
            // By default, return an error should nothing override it
            string userStatusValue = AppConstants.Tenders_UserStatus_Error;

            // TODO: eSourcing validation rules go here

            return userStatusValue;
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
