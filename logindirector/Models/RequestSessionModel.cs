namespace logindirector.Models
{
    // Representation of a user's HTTP request to be held until it's ready to be actioned - only contains the information we need (can't store HttpRequest itself)
    public class RequestSessionModel
    {
        public string domain { get; set; }

        public string protocol { get; set; }

        public string requestedPath { get; set; }

        public string httpFormat { get; set; }
    }
}
