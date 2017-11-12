using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSockets_Echo.WsListener
{

	public class WsMessageHandler
	{
		private readonly IWebsocketManager _wsManager;

		public WsMessageHandler(IWebsocketManager wsManager)
		{
			_wsManager = wsManager;
		}



		/// <summary>
		/// Parse message and define its type. 
		/// Pass message further for processing according to this type.
		/// </summary>
		/// <param name="message">Stringified ClientMesage.</param>
		/// <param name="socket">WebSocket object.</param>
		/// <param name="ct">CancellationToken.</param>
		public async Task ProcessMessage(string message, WebSocket socket, CancellationToken ct = default(CancellationToken))
		{
			WsMesage msg;
			try
			{
				msg = WsMesage.Parse(message);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! [ClientMessageHandler.ProcessMessage] " +
					$"Cannot parse message: {message}. Exception: {ex.Message}.)");
				return;
			}

			if (!msg?.IsValid ?? true)
				return;

			switch (msg.Type.ToLower())
			{
				case "chat_from_merchant":
					await ProcessFrontendChatMessage(socket, msg, ct);
					break;
				default:
					Console.WriteLine($"Couldn't find match action for message type '{msg.Type}'");
					break;
			}
		}

		/// <summary>
		/// Parses message. Sends its body to all receiver's sockets and resends to all
		/// sender's sockets except this current one.
		/// </summary>
		/// <param name="socket">WebSocket object.</param>
		/// <param name="msg">ClientMesage.</param>
		/// <param name="ct">CancellationToken.</param>
		private async Task ProcessFrontendChatMessage(WebSocket socket, WsMesage msg, CancellationToken ct = default(CancellationToken))
		{
			ChatMsgFE chatMessage = msg.Data as ChatMsgFE;
			chatMessage = JsonConvert.DeserializeObject<ChatMsgFE>(JsonConvert.SerializeObject(msg.Data));
			if (!chatMessage?.IsValid ?? true)
				return;

			WsMesage messageToSend = new WsMesage
			{
				Type = "chat",
				Data = msg.Data
			};

			await _wsManager.BroadcastAsync(messageToSend.ToString(),
				recipients: new List<string>() { chatMessage.From, chatMessage.To },
				exceptSockets: new List<WebSocket>() { socket });
		}
	}
}
