using System.Threading.Tasks;
using logindirector.Models.AdaptorService;

namespace logindirector.Services
{
    // Interface class for AdaptorClientServices
    public interface IAdaptorClientServices
    {
        Task<AdaptorUserModel> GetUserInformation(string username);

        Task<string> PerformAdaptorRequest(string routeUri);
    }
}
