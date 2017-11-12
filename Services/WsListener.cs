using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSockets_Echo.WsListener
{
	public class WsClient
	{
		private readonly WsListener.WsMessageHandler _handler;
		private readonly IWebsocketManager _wsManager;

		public WsClient(WsListener.WsMessageHandler handler, IWebsocketManager wsManager)
		{
			_wsManager = wsManager;
			_handler = handler;
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
				await _handler.ProcessMessage(response, socket, ct);
			}

			await KillSocket(socket, ct);
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
			await Task.Run(() => _wsManager.RemoveSocket(socket));
			await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
			socket.Dispose();
		}
	}
}
