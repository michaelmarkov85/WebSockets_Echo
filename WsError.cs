using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSockets_Echo
{
	public class WsError
	{
		public int Code { get; private set; }
		public string Message { get; private set; }

		public WsError(int code, string message)
		{
			Code = code;
			Message = message;
		}

		public WsError(WsErrors code, string message)
			: this((int)code, message) { }

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	public enum WsErrors : int
	{
		UNKNOWN = 0,

	}
}
