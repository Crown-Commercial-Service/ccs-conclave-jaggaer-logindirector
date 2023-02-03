using System;
using System.Collections.Generic;

namespace logindirector.Models.TendersApi
{
    // Model for the role detail of a response from the Tenders API.  Data should be a list of valid roles currently applied to the user in Jaegger
    public class RolesResponseModel
	{
		public List<string> roles { get; set; }
	}
}

