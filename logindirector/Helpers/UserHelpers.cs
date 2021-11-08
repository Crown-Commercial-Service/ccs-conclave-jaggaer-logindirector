using System.Collections.Generic;
using System.Linq;
using logindirector.Constants;
using logindirector.Models.AdaptorService;

namespace logindirector.Helpers
{
    public class UserHelpers : IHelpers
    {
        public bool HasValidUserRoles(AdaptorUserModel userModel)
        {
            // Check whether the user has a valid role for this application
            if (userModel.coreRoles != null && userModel.coreRoles.Any())
            {
                List<AdaptorUserRoleModel> relevantRoles = userModel.coreRoles.Where(r => r.serviceClientName == AppConstants.Adaptor_ClientRoleAssignment).ToList();

                if (relevantRoles != null && relevantRoles.Any())
                {
                    // Valid role found - return true
                    return true;
                }
            }

            // Also check against additionalRoles incase a valid role has been added by means of a usergroup
            if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
            {
                List<string> relevantRoles = userModel.additionalRoles.Where(r => r == AppConstants.RoleKey_JaeggerSupplier || r == AppConstants.RoleKey_JaeggerBuyer || r == AppConstants.RoleKey_CatUser).ToList();

                if (relevantRoles != null && relevantRoles.Any())
                {
                    // Valid role found - return true
                    return true;
                }
            }

            // No valid roles found for this user - return false
            return false;
        }
    }
}
