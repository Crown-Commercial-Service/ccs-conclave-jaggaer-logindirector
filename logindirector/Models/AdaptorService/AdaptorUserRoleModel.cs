using Newtonsoft.Json;

namespace logindirector.Models.AdaptorService
{
    // Model representing a single user role, returned from the adaptor service
    public class AdaptorUserRoleModel
    {
        [JsonProperty("roleId")]
        public string roleId { get; set; }

        [JsonProperty("roleName")]
        public string roleName { get; set; }

        [JsonProperty("roleKey")]
        public string roleKey { get; set; }

        [JsonProperty("serviceClientName")]
        public string serviceClientName { get; set; }
    }
}
