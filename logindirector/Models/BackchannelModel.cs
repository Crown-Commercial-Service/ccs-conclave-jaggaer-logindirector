namespace logindirector.Models
{
    // Model to hold values necessary to setup the Backchannel Logout system
    public class BackchannelModel
    {
        public string ClientId { get; set; }

        public string SessionState { get; set; }

        public string SecurityApiUrl { get; set; }

        public string RedirectUrl { get; set; }
    }
}
