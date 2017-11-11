using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebSockets_Echo.ClientMessages
{

	public class ClientMessageHandler
	{
		private WebsocketManager _wsm;

		public ClientMessageHandler(WebsocketManager wsm)
		{
			_wsm = wsm;
		}

		/// <summary>
		/// In infinite loop receives messages from connected FrontEnd 
		/// and passes them to procession. When WebSocketState becomes not Open
		/// removes socket from WebsocketManager collection, closes connection
		/// and disposes socket.
		/// </summary>
		/// <param name="socket">A socket object.</param>
		/// <param name="ct">CancellationToken from outside methods.</param>
		/// <returns>Void.</returns>
		public async Task Listen(WebSocket socket, CancellationToken ct)
		{
			while (true)
			{
				if (ct.IsCancellationRequested)
					break;

				string response = await ReceiveStringAsync(socket, ct);
				if (string.IsNullOrEmpty(response))
				{
					if (socket.State != WebSocketState.Open)
					{
						break;
					}
					continue;
				}
				await ProcessMessage(response, socket, ct);
			}

			await KillSocket(socket, ct);
		}

		/// <summary>
		/// Parse message and define its type. 
		/// Pass message further for processing according to this type.
		/// </summary>
		/// <param name="message">Stringified ClientMesage.</param>
		/// <param name="socket">WebSocket object.</param>
		/// <param name="ct">CancellationToken.</param>
		private async Task ProcessMessage(string message, WebSocket socket, CancellationToken ct = default(CancellationToken))
		{
			ClientMesage msg;
			try
			{
				msg = ClientMesage.Parse(message);
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
		private async Task ProcessFrontendChatMessage(WebSocket socket, ClientMesage msg, CancellationToken ct = default(CancellationToken))
		{
			ChatMsgFE chatMessage = msg.Data as ChatMsgFE;
			chatMessage = JsonConvert.DeserializeObject<ChatMsgFE>(JsonConvert.SerializeObject(msg.Data));
			if (!chatMessage?.IsValid ?? true)
				return;

			// Getting all opened receiver sockets
			List<WebSocket> to = _wsm.GetSockets(chatMessage.To);

			// Getting all opened sender sockets, but not including current one
			// through which the message was sent, so sender's active browser tab
			// won't show sent message twice.
			List<WebSocket> from = _wsm.GetSockets(chatMessage.From);
			if(!from.Remove(socket))
				return;

			// Combining sockets collection to send message
			to.AddRange(from);
			List<Task> tasks = new List<Task>();

			foreach (var s in to)
			{
				tasks.Add(_wsm.SendStringAsync(s, chatMessage.ToString(), ct));
			}
			await Task.WhenAll(tasks.ToArray());
		}

		/// <summary>
		/// Receives WebSocket frames to ArraySegment buffer, writes into memory stream in a loop
		/// until EndOfMessage. Then reads stream into a string.
		/// </summary>
		/// <param name="socket">WebSocket object.</param>
		/// <param name="ct">CancellationToken.</param>
		/// <returns>Combined message.</returns>
		private static async Task<string> ReceiveStringAsync(WebSocket socket, CancellationToken ct = default(CancellationToken))
		{
			var buffer = new ArraySegment<byte>(new byte[1024 * 4]);
			using (var ms = new MemoryStream())
			{
				WebSocketReceiveResult result;
				do
				{
					ct.ThrowIfCancellationRequested();

					result = await socket.ReceiveAsync(buffer, ct);
					ms.Write(buffer.Array, buffer.Offset, result.Count);
				}
				while (!result.EndOfMessage);

				ms.Seek(0, SeekOrigin.Begin);
				if (result.MessageType != WebSocketMessageType.Text)
				{
					return null;
				}

				// Encoding UTF8: https://tools.ietf.org/html/rfc6455#section-5.6
				using (var reader = new StreamReader(ms, Encoding.UTF8))
				{
					return await reader.ReadToEndAsync();
				}
			}
		}

		/// <summary>
		/// Removes socket from WebsocketManager collection, closes connection
		/// and disposes socket.
		/// </summary>
		/// <param name="socket">WebSocket object.</param>
		/// <param name="ct">CancellationToken.</param>
		private async Task KillSocket(WebSocket socket, CancellationToken ct)
		{
			await Task.Run(() => _wsm.RemoveSocket(socket));
			await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
			socket.Dispose();
		}
	}
}
