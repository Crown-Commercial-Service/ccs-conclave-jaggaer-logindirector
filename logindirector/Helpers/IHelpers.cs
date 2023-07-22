using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace logindirector.Helpers
{
    public interface IHelpers
    {
        ErrorViewModel BuildErrorModelForUser(string sessionUserRequestJson);

        Task<bool> DoesUserHaveValidSession(HttpContext httpContext, string userSid);
    }
}
