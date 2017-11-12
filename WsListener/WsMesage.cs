using System;
using Newtonsoft.Json;

namespace WebSockets_Echo.WsListener
{
	[Serializable]
	public class WsMesage
	{
		public string Type { get; set; }
		public dynamic Data { get; set; }

		public WsMesage()
		{		}

		public WsMesage( string type, dynamic data)
		{
			Type = type;
			Data = data;
		}


		[JsonIgnore]
		public bool IsValid => !string.IsNullOrWhiteSpace(Type) && Data != null;

		/// <summary>
		/// Deserializes string into ClientMesage Object if valid string. 
		/// Otherwise throws exception;
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		public static WsMesage Parse(string message)
		{
			return JsonConvert.DeserializeObject<WsMesage>(message);
		}

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
