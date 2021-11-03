using System;
using Newtonsoft.Json;

namespace logindirector.Models.AdaptorService
{
    // Model for user information returned from the adaptor service
    public class AdaptorUserModel
    {
        [JsonProperty("userDetailId")]
        public string userId { get; set; }

        [JsonProperty("firstName")]
        public string givenName { get; set; }

        [JsonProperty("lastName")]
        public string familyName { get; set; }

        // TODO: Build out the rest of this model and its submodels

        // roles

        // additional roles

        // contact info

        // org info
    }
}
