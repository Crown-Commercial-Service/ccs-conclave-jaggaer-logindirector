using System.Collections.Generic;

namespace logindirector.Models.TendersApi
{
    // Model for a list of existing user roles for this user.  Data supplied via Tenders API GET response in instances of 200 OK
    public class ExistingUserRolesModel
    {
        public List<string> roles { get; set; }
    }
}
