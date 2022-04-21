using logindirector.Models;
using logindirector.Models.AdaptorService;

namespace logindirector.Helpers
{
    public interface IHelpers
    {
        bool HasValidUserRoles(AdaptorUserModel userModel);

        ErrorViewModel BuildErrorModelForUser(string sessionUserRequestJson);
    }
}
