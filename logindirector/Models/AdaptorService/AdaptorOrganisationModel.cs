using Newtonsoft.Json;

namespace logindirector.Models.AdaptorService
{
    // Model for organisation information returned from the adaptor service against a user
    public class AdaptorOrganisationModel
    {
        [JsonProperty("scheme")]
        public string schemeType { get; set; }

        [JsonProperty("id")]
        public string organisationIds { get; set; }

        [JsonProperty("legalName")]
        public string legalName { get; set; }
    }
}
