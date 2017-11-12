using Newtonsoft.Json;

namespace WebSockets_Echo.RmqListener
{
	public class RmqMessage
	{
		public string Type { get; set; }
		public string Recipient { get; set; }
		public dynamic Data { get; set; }

		public bool IsValid => !string.IsNullOrWhiteSpace(Type) && Data != null;

		/// <summary>
		/// Deserializes string into ClientMesage Object if valid string. 
		/// Otherwise throws exception;
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		public static RmqMessage Parse(string message)
		{
			// TODO: Implement TryParse
			return JsonConvert.DeserializeObject<RmqMessage>(message);
		}
	}
}
