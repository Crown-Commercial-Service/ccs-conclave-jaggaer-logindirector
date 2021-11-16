using Newtonsoft.Json;
using System.Collections.Generic;

namespace logindirector.Models.AdaptorService
{
    // Model for user information returned from the adaptor service
    public class AdaptorUserModel
    {
        [JsonProperty("userDetailId")]
        public string userId { get; set; }

        [JsonProperty("userName")]
        public string emailAddress { get; set; }

        [JsonProperty("firstName")]
        public string givenName { get; set; }

        [JsonProperty("lastName")]
        public string familyName { get; set; }

        [JsonProperty("rolePermissionInfo")]
        public List<AdaptorUserRoleModel> coreRoles { get; set; }

        [JsonProperty("additionalRoles")]
        public List<string> additionalRoles { get; set; }

        [JsonProperty("userContactPoints")]
        public List<AdaptorUserContactModel> contactInfo { get; set; }

        [JsonProperty("organisationAdditionalIdentifiers")]
        public List<AdaptorOrganisationModel> orgInfo { get; set; }
    }
}
