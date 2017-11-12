using System;

namespace WebSockets_Echo
{
	public class ChatMsgFE
	{
		public string From { get; set; }
		public string To { get; set; }

		public string Body { get; set; }
		public DateTime Created { get; set; }

		public bool IsValid
		{
			get
			{
				return !string.IsNullOrWhiteSpace(From) && Guid.TryParse(From, out var f)
					&& !string.IsNullOrWhiteSpace(To) && Guid.TryParse(To, out var t)
					&& !string.IsNullOrEmpty(Body);
			}
		}

		public override string ToString()
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(this);
		}
	}
}
