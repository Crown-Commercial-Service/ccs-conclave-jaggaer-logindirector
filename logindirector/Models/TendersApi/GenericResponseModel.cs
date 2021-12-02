using System.Net;

namespace logindirector.Models.TendersApi
{
    // Model for a response from the Tenders API.  Data held as status code and string - it gets mapped to full models later as necessary
    public class GenericResponseModel
    {
        public HttpStatusCode StatusCode { get; set; }

        public string ResponseValue { get; set; }
    }
}
