using Newtonsoft.Json;

namespace logindirector.Models.AdaptorService
{
    // Model for a single contact mapping returned from the adaptor service for a user
    public class AdaptorContactModel
    {
        [JsonProperty("contactId")]
        public int contactId { get; set; }

        [JsonProperty("contactType")]
        public string contactType { get; set; }

        [JsonProperty("contactValue")]
        public string contactValue { get; set; }
    }
}
