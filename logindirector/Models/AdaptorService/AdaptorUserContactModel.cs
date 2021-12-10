using Newtonsoft.Json;
using System.Collections.Generic;

namespace logindirector.Models.AdaptorService
{
    //Model for user contact information returned from the adaptor service
    public class AdaptorUserContactModel
    {
        [JsonProperty("contactPointId")]
        public int contactPointId { get; set; }

        [JsonProperty("contactPointReason")]
        public string contactGroupType { get; set; }

        [JsonProperty("contactPointName")]
        public string contactPointName { get; set; }

        [JsonProperty("contacts")]
        public List<AdaptorContactModel> contactDetails { get; set; }
    }
}
