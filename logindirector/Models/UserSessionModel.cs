using System;

namespace logindirector.Models
{
    // Representation of a user's session for the central cache
    public class UserSessionModel
    {
        public string userEmail { get; set; }

        public DateTime sessionStart { get; set; }
    }
}
