using System.Threading.Tasks;
using System.Net.Http;
using logindirector.Models.TendersApi;

namespace logindirector.Services
{
    /**
     * Interface class for TendersClientServices
     */
    public interface ITendersClientServices
    {
        Task<UserStatusModel> GetUserStatus(string username, string accessToken, string domain);

        Task<UserCreationModel> CreateJaeggerUser(string username, string accessToken);

        Task<GenericResponseModel> PerformTendersRequest(string routeUri, string accessToken, HttpMethod method);
    }
}
