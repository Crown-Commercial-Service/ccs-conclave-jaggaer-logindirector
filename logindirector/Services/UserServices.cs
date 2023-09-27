using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public IAdaptorClientServices _adaptorClientServices;

        public UserServices(IConfiguration configuration, IAdaptorClientServices adaptorClientServices)
        {
            _configuration = configuration;
            _adaptorClientServices = adaptorClientServices;
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
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_Evaluator) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerBuyer) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_Evaluator) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerSupplier) != null))
                {
                    // Valid core role / domain configuration found - return true
                    return true;
                }
            }

            if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
            {
                if ((requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_CatUser) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_Evaluator) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerBuyer) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_Evaluator) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerSupplier) != null))
                {
                    // Valid additional role / domain configuration found - return true
                    return true;
                }
            }

            // No valid role / domain configuration found for this user - return false
            return false;
        }

        /**
         * Determines the role state of an account configured for eSourcing in the SSO service
         * Validates what role combinations they have and sends back a representative state to inform other areas of this
         */
        public async Task<string> GetEsourcingSsoRoleState(string username)
        {
            AdaptorUserModel userModel = await _adaptorClientServices.GetUserInformation(username);

            if (userModel != null)
            {
                bool buyerFound = false,
                    supplierFound = false;

                // Populate our booleans based on the role setup - we need to check both core and additional roles
                if (userModel.coreRoles != null && userModel.coreRoles.Any())
                {
                    if (userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerBuyer) != null)
                    {
                        buyerFound = true;
                    }

                    if (userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerSupplier) != null)
                    {
                        supplierFound = true;
                    }
                }

                if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
                {
                    if (userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerBuyer) != null)
                    {
                        buyerFound = true;
                    }

                    if (userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerSupplier) != null)
                    {
                        supplierFound = true;
                    }
                }

                // First, check whether the account has BOTH buyer and supplier roles
                if (buyerFound && supplierFound)
                {
                    return AppConstants.RoleSetup_EsourcingBothRoles;
                }


                // They must not have both roles, so now check if they have ONLY the buyer role
                if (buyerFound && !supplierFound)
                {
                    return AppConstants.RoleSetup_EsourcingBuyerOnly;
                }


                // They must not have only the buyer either, so now check for ONLY the supplier role
                if (supplierFound && !buyerFound)
                {
                    return AppConstants.RoleSetup_EsourcingSupplierOnly;
                }
            }


            // If we've gotten this far something is very wrong - return an error state
            return AppConstants.RoleSetup_NoRoles;
        }


        /**
         * Determines the role state of an account configured for CAS in the SSO service
         * Validates what role combinations they have and sends back a representative state to inform other areas of this
         */
        public async Task<string> GetCasSsoRoleState(string username)
        {
            AdaptorUserModel userModel = await _adaptorClientServices.GetUserInformation(username);

            if (userModel != null)
            {
                bool roleFound = false;

                // Populate our boolean based on the role setup - we need to check both core and additional roles
                if (userModel.coreRoles != null && userModel.coreRoles.Any())
                {
                    if (userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_CatUser) != null)
                    {
                        roleFound = true;
                    }
                }

                if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
                {
                    if (userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_CatUser) != null)
                    {
                        roleFound = true;
                    }
                }

                // Check if the user has the role
                if (roleFound)
                {
                    return AppConstants.RoleSetup_CasRole;
                }
            }


            // If we've gotten this far something is very wrong - return an error state
            return AppConstants.RoleSetup_NoRoles;
        }
    }
}

