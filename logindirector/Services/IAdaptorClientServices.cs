using System.Threading.Tasks;
using logindirector.Models.AdaptorService;

namespace logindirector.Services
{
    // TODO: Refactor this into a generic interface for all services, similar to IBusinessLogicClient principle?
    public interface IAdaptorClientServices
    {
        Task<AdaptorUserModel> GetUserInformation(string username);

        Task<string> PerformAdaptorRequest(string routeUri);
    }
}
