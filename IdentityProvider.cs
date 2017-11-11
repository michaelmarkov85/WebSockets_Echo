using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSockets_Echo
{
	public class IdentityProvider
	{
		public string GetOwner(string token)
		{
			// Implement more sophisticated logic
			return token; 
		}
	}
}
