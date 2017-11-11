using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSockets_Echo.ClientMessages
{
	[Serializable]
	public class ClientMesage
	{
		public string Type { get; set; }
		public dynamic Data { get; set; }

		public bool IsValid => !string.IsNullOrWhiteSpace(Type) && Data != null;

		/// <summary>
		/// Deserializes string into ClientMesage Object if valid string. 
		/// Otherwise throws exception;
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		public static ClientMesage Parse(string message)
		{
			return JsonConvert.DeserializeObject<ClientMesage>(message);
		}

	}
}
