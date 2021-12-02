using System.Threading.Tasks;
using logindirector.Models.TendersApi;

namespace logindirector.Services
{
    // Interface class for TendersClientServices
    public interface ITendersClientServices
    {
        Task<UserStatusModel> GetUserStatus(string username, string accessToken);

        Task<GenericResponseModel> PerformTendersRequest(string routeUri, string accessToken);
    }
}
