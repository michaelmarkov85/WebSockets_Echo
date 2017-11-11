using System;

namespace WebSockets_Echo
{
	public class Msg : WebsocketMessage
	{
		public string Recipient { get; set; }


		public override bool IsValid
		{
			get
			{
				bool result = (base.IsValid
						&& !string.IsNullOrWhiteSpace(Recipient)
						&& Guid.TryParse(Recipient, out Guid g));
				return result;
			}
		}
	}
}
