using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSockets_Echo
{
	[Serializable]
	public class WebsocketMessage
	{
		public string Type { get; set; }
		public dynamic Data { get; set; }

		[JsonIgnore]
		public virtual bool IsValid
		{
			get
			{
				bool result = (!string.IsNullOrWhiteSpace(Type));
				return result;
			}
		}

		public override string ToString()
		{
			JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
			{
				DateTimeZoneHandling = DateTimeZoneHandling.Utc,
			};

			// When serializing base class object that was made by up-cast of derived class
			// all properties of derived class gets serialized as well. A workaround is to
			// create a new instance of base class an copy properties of derived one.
			WebsocketMessage wm = new WebsocketMessage()
			{
				Type = this.Type,
				Data = this.Data
			};

			string result = JsonConvert.SerializeObject(wm, jsonSerializerSettings);
			return result;
		}
	}
}
