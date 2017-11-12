using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSockets_Echo.RmqListener
{
	public class RmqMessageHandler
	{
		private readonly IWebsocketManager _wsManager;

		public RmqMessageHandler(IWebsocketManager wsManager)
		{
			_wsManager = wsManager;
		}

		public async Task ProcessMessage(string message, CancellationToken ct = default(CancellationToken))
		{
			var jsonSettings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };

			RmqMessage msg = RmqMessage.Parse(message);

			if (!msg?.IsValid ?? true)
				return;

			switch (msg.Type.ToLower())
			{
				case "notification":
					await ProcessRmqMessage(msg);
					break;
				default:
					await BroadcastErrorMsgToAll(msg, $"Couldn't find match action for message type '{msg.Type}'");
					break;
			}
		}

		private async Task BroadcastErrorMsgToAll(RmqMessage msg, string text)
		{
			if (!msg?.IsValid ?? false)
			{
				Console.WriteLine($"Couldn't find match action for message type '{msg.Type}'");
				return;
			}

			WsListener.WsMesage messageToSend = new WsListener.WsMesage
			{
				Type = "error",
				Data = new { Message = text, Data = msg.Data }
			};

			await _wsManager.BroadcastToAllAsync(messageToSend.ToString());
		}

		private async Task ProcessRmqMessage(RmqMessage msg)
		{

			if (!msg?.IsValid ?? false)
			{
				Console.WriteLine($"Couldn't find match action for message type '{msg.Type}'");
				return;
			}

			string recipient = msg.Recipient;

			WsListener.WsMesage messageToSend = new WsListener.WsMesage
			{
				Type = "notification",
				Data = msg.Data
			};

			await _wsManager.SendAsync(messageToSend.ToString(), recipient);
		}
	}
}
