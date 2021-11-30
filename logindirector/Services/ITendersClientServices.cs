using System.Threading.Tasks;

namespace logindirector.Services
{
    // Interface class for TendersClientServices
    public interface ITendersClientServices
    {
        Task<string> GetUserStatus(string username);

        Task<string> PerformTendersRequest(string routeUri);
    }
}
