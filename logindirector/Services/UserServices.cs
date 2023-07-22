using System;
using System.Collections.Generic;
using System.Linq;
using logindirector.Constants;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace logindirector.Services
{
    /** 
     * Service Client for operations against the user passed back to us by the SSO service
     */
    public class UserServices : IUserServices
	{
        public IConfiguration _configuration { get; }

        public UserServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /**
         * Checks where a user, before Login Director has processed them, has valid roles to allow us to continue with processing them
         */
        public bool DoesUserHaveValidRolePreProcessing(AdaptorUserModel userModel, RequestSessionModel requestSessionModel)
        {
            // Check whether the user has a valid role / domain configuration for this application via both coreRoles and additionalRoles, and session request object
            if (userModel.coreRoles != null && userModel.coreRoles.Any())
            {
                if ((requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_CatUser) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerBuyer) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerSupplier) != null))
                {
                    // Valid core role / domain configuration found - return true
                    return true;
                }
            }

            if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
            {
                if ((requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_CatUser) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerBuyer) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerSupplier) != null))
                {
                    // Valid additional role / domain configuration found - return true
                    return true;
                }
            }

            // No valid role / domain configuration found for this user - return false
            return false;
        }
    }
}

