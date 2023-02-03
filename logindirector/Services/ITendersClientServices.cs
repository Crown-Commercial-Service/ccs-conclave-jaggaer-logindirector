using System.Threading.Tasks;
using System.Net.Http;
using logindirector.Models.TendersApi;
using logindirector.Models.AdaptorService;

namespace logindirector.Services
{
    // Interface class for TendersClientServices
    public interface ITendersClientServices
    {
        Task<UserStatusModel> GetUserStatus(string username, string accessToken, AdaptorUserModel userModel = null, bool isPostProcessing = false);

        Task<UserCreationModel> CreateJaeggerUser(string username, string accessToken);

        Task<GenericResponseModel> PerformTendersRequest(string routeUri, string accessToken, HttpMethod method);
    }
}
