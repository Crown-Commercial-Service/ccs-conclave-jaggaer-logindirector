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
using Newtonsoft.Json;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using System.Security.Claims;
using System.Collections.Generic;

namespace logindirector.Services
{
    /**
     * Service Client for the Tenders API service - where Jaegger operations are performed against
     */
    public class TendersClientServices : ITendersClientServices
    {
        public IConfiguration _configuration { get; }
        private readonly IHttpContextAccessor _context;
        public IUserServices _userServices;

        public TendersClientServices(IConfiguration configuration, IHttpContextAccessor context, IUserServices userServices)
        {
            _configuration = configuration;
            _context = context;
            _userServices = userServices;
        }

        /**
         * Retrieves the status of a Jaegger user matching the authenticated user before they have been processed by the service
         */
        public async Task<UserStatusModel> GetUserStatusPreProcessing(string username, string accessToken, string domain)
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

        /**
         * Retrieves and check the status of a Jaegger user matching the authenticated user after they have been processed by the service
         * Different rules apply here than in the initial check
         */
        public async Task<UserStatusModel> GetUserStatusPostProcessing(string username, string accessToken, string domain)
        {
            UserStatusModel model = null;

            try
            {
                // Fetch the information we need from the User GET route
                string userRouteUri = _configuration.GetValue<string>("TendersApi:ApiDomain") + _configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                GenericResponseModel responseModel = await PerformTendersRequest(userRouteUri + HttpUtility.UrlEncode(username), accessToken, HttpMethod.Get);

                if (responseModel != null)
                {
                    // We now need to map our response to a useful model to return - different rules apply depending on the service we're trying to access
                    model = new UserStatusModel();

                    if (responseModel.StatusCode == HttpStatusCode.OK)
                    {
                        // The processing appears to have worked fine, but we need to check that their role state matches what we expect
                        string requestDetails = _context.HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey);

                        if (!String.IsNullOrWhiteSpace(requestDetails))
                        {
                            RequestSessionModel requestModel = JsonConvert.DeserializeObject<RequestSessionModel>(requestDetails);

                            model.UserStatus = await ValidateTendersResponseMatchesExpectedPostProcessingState(domain, responseModel, requestModel, username);
                        }
                        else
                        {
                            // We can't access the user's information from session - we therefore have to assume there's an error
                            RollbarLocator.RollbarInstance.Info("Cannot access user information from session to validate processing");

                            model.UserStatus = AppConstants.Tenders_PostProcessingStatus_Error;
                        }
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.NotFound && responseModel.ResponseValue.Contains("not found in Jaggaer"))
                    {
                        // The processing appears to have failed - they should have an account by now
                        model.UserStatus = AppConstants.Tenders_PostProcessingStatus_MergeFailure;
                    }
                    else if (responseModel.StatusCode == HttpStatusCode.Conflict || responseModel.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // There's a role mismatch between the user's PPG roles and what Tenders is finding in Jaegger
                        model.UserStatus = AppConstants.Tenders_PostProcessingStatus_Conflict;
                    }
                    else
                    {
                        // This is an unexpected error response from Tenders that we can't handle
                        RollbarLocator.RollbarInstance.Info("Invalid Tenders Response - StatusCode: " + responseModel.StatusCode + " --- ResponseValue: " + responseModel.ResponseValue);

                        model.UserStatus = AppConstants.Tenders_PostProcessingStatus_Error;
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
         * Validates a post-processing OK status response from Tenders to be sure that it's really correct based on the service being accessed
         */
        public async Task<string> ValidateTendersResponseMatchesExpectedPostProcessingState(string domain, GenericResponseModel responseModel, RequestSessionModel requestModel, string username)
        {
            // Status suggests the merge was successful, but we need to check that this is actually true based on reported account state
            ExistingUserRolesModel existingRolesModel = JsonConvert.DeserializeObject<ExistingUserRolesModel>(responseModel.ResponseValue);

            if (existingRolesModel != null && existingRolesModel.roles.Any())
            {
                if (domain == _configuration.GetValue<string>("ExitDomains:CatDomain"))
                {
                    // We need to know the user's SSO service role state to process this
                    string userSsoRoleState = await _userServices.GetCasSsoRoleState(username);

                    if (!String.IsNullOrWhiteSpace(userSsoRoleState))
                    {
                        return ValidatePostProcessingTendersResponseForCas(userSsoRoleState, existingRolesModel);
                    }

                    // If we get to here we can't fetch the current user roles, so we can't tell what we should be checking for, so return an error state
                    RollbarLocator.RollbarInstance.Info("Can't fetch roles from PPG for user post-processing state check");

                    return AppConstants.Tenders_PostProcessingStatus_Error;
                }
                else
                {
                    // We need to know the user's SSO service role state to process this
                    string userSsoRoleState = await _userServices.GetEsourcingSsoRoleState(username);

                    if (!String.IsNullOrWhiteSpace(userSsoRoleState))
                    {
                        return ValidatePostProcessingTendersResponseForEsourcing(userSsoRoleState, existingRolesModel);
                    }

                    // If we get to here we can't fetch the current user roles, so we can't tell what we should be checking for, so return an error state
                    RollbarLocator.RollbarInstance.Info("Can't fetch roles from PPG for user post-processing state check");

                    return AppConstants.Tenders_PostProcessingStatus_Error;
                }
            }

            // If we've got to here, the details of the response don't appear to back up that the status code is actually "OK".  We have to assume merge failure
            return AppConstants.Tenders_PostProcessingStatus_MergeFailure;
        }

        /**
         * Validates a post-processing Tenders response for the CAS service
         */
        internal string ValidatePostProcessingTendersResponseForCas(string userSsoRoleState, ExistingUserRolesModel existingRolesModel)
        {
            if (userSsoRoleState != AppConstants.RoleSetup_CasRole)
            {
                // User does not have the CAS role - indicates the role setup must have changed during processing
                return AppConstants.Tenders_PostProcessingStatus_RoleMismatch;
            }
            else if (existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer))
            {
                // They've a buyer account, so they're good to go
                return AppConstants.Tenders_PostProcessingStatus_Valid;
            }
            else if (existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier) && !existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Evaluator))
            {
                // They've only got a supplier account - indicates the wrong account type was merged
                return AppConstants.Tenders_PostProcessingStatus_WrongType;
            }
            else if (existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Evaluator) && !existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier))
            {
                // They've only got an evaluator account - indicates the merge needs to be re-tried with a different account
                return AppConstants.Tenders_PostProcessingStatus_EvaluatorMerged;
            }

            // If we've got to here, the details of the response don't appear to back up that the status code is actually "OK".  We have to assume merge failure
            return AppConstants.Tenders_PostProcessingStatus_MergeFailure;
        }

        /**
         * Validates a post-processing Tenders response for the eSourcing service
         */
        internal string ValidatePostProcessingTendersResponseForEsourcing(string userSsoRoleState, ExistingUserRolesModel existingRolesModel)
        {
            if ((userSsoRoleState == AppConstants.RoleSetup_EsourcingBuyerOnly && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer) && existingRolesModel.roles.Count == 1) ||
                            (userSsoRoleState == AppConstants.RoleSetup_EsourcingSupplierOnly && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier) && existingRolesModel.roles.Count == 1) ||
                            (userSsoRoleState == AppConstants.RoleSetup_EsourcingBothRoles && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer) && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier)))
            {
                // SSO service says one thing only, as does Tenders - this is valid
                return AppConstants.Tenders_PostProcessingStatus_Valid;
            }
            else if ((userSsoRoleState == AppConstants.RoleSetup_EsourcingBuyerOnly && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier) && existingRolesModel.roles.Count == 1) ||
                (userSsoRoleState == AppConstants.RoleSetup_EsourcingBothRoles && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Evaluator) && existingRolesModel.roles.Count == 1))
            {
                // SSO service says buyer only, Tenders says supplier only - a merge failure has occurred
                // Also true if SSO service says both roles but Tenders says evaluator
                return AppConstants.Tenders_PostProcessingStatus_MergeFailure;
            }
            else if ((userSsoRoleState == AppConstants.RoleSetup_EsourcingBuyerOnly || userSsoRoleState == AppConstants.RoleSetup_EsourcingSupplierOnly) && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Evaluator) && existingRolesModel.roles.Count == 1)
            {
                // SSO service says buyer only, Tenders says evaluator only - indicates the merge needs to be re-tried with a different account
                return AppConstants.Tenders_PostProcessingStatus_EvaluatorMerged;
            }
            else if (((userSsoRoleState == AppConstants.RoleSetup_EsourcingBuyerOnly || userSsoRoleState == AppConstants.RoleSetup_EsourcingSupplierOnly) && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer) && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier))
                || (userSsoRoleState == AppConstants.RoleSetup_EsourcingSupplierOnly && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer) && existingRolesModel.roles.Count == 1))
            {
                // SSO service says one thing, Tenders says another - indicates a role mismatch
                return AppConstants.Tenders_PostProcessingStatus_RoleMismatch;
            }
            else if ((userSsoRoleState == AppConstants.RoleSetup_EsourcingBothRoles && (existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer) || existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier)) && existingRolesModel.roles.Count == 1) ||
                (userSsoRoleState == AppConstants.RoleSetup_EsourcingBothRoles && existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Evaluator) && (existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Supplier) || existingRolesModel.roles.Contains(AppConstants.ExistingRoleKey_Buyer)) && existingRolesModel.roles.Count == 2))
            {
                // The user does not have enough accounts merged
                return AppConstants.Tenders_PostProcessingStatus_NotEnoughAccounts;
            }

            // If we've got to here, the details of the response don't appear to back up that the status code is actually "OK".  We have to assume merge failure
            return AppConstants.Tenders_PostProcessingStatus_MergeFailure;
        }
    }
}
