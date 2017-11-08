using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebSockets_Echo
{
	public class NotificationWsMiddleware
	{
		private static ConcurrentDictionary<string, List<string>> _owners = new ConcurrentDictionary<string, List<string>>();

		private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

		private readonly RequestDelegate _next;
		public NotificationWsMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task Invoke(HttpContext context)
		{
			if (!context.WebSockets.IsWebSocketRequest)
			{
				await _next.Invoke(context);
				return;
			}
			CancellationToken ct = context.RequestAborted;
			WebSocket currentSocket = await context.WebSockets.AcceptWebSocketAsync();

			string ownerId = context.Request.Query["owner"].ToString();
			string socketId = Guid.NewGuid().ToString();

			// adding owner->socketIds 
			if (_owners.TryGetValue(ownerId, out List<string> socketIds))
				socketIds.Add(socketId);
			else
				_owners.TryAdd(ownerId, new List<string> { socketId });
			// TODO: make remove logic

			_sockets.TryAdd(socketId, currentSocket);
			await Echo(currentSocket, socketId, ct);
		}

		private static async Task Echo(WebSocket currentSocket, string socketId, CancellationToken ct)
		{
			while (true)
			{
				if (ct.IsCancellationRequested)
				{
					break;
				}

				var response = await ReceiveStringAsync(currentSocket, ct);
				if (string.IsNullOrEmpty(response))
				{
					if (currentSocket.State != WebSocketState.Open)
					{
						break;
					}

					continue;
				}

				await ProcessMessage(response, ct);
			}

			WebSocket dummy;
			_sockets.TryRemove(socketId, out dummy);

			await currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
			currentSocket.Dispose();
		}

		internal static async Task ProcessMessage(string message, CancellationToken ct = default(CancellationToken))
		{
			var f = message.Split('_');
			if (f.Length == 3) // correct message - from, to and text
			{
				await Task.WhenAll(BroadcastMessage(f[2], f[1]), BroadcastMessage(f[2], f[0]));
			}
			else
				await BroadcastMessageToAll("Wrong message format: " + message);
		}

		internal static async Task BroadcastMessage(string data, string ownerId, CancellationToken ct = default(CancellationToken))
		{
			if (_owners.TryGetValue(ownerId, out List<string> socketIds))
				if (socketIds!= null && socketIds.Count>0)
					foreach (string sId in socketIds)
						if (_sockets.TryGetValue(sId, out WebSocket socket))
							if (socket != null || socket.State == WebSocketState.Open)
								await SendStringAsync(socket, data, ct);
		}
		internal static async Task BroadcastMessageToAll(string data, CancellationToken ct = default(CancellationToken))
		{
			foreach (var s in _sockets)
				if(s.Value.State == WebSocketState.Open)
					await SendStringAsync(s.Value, data, ct);
		}

		private static Task SendStringAsync(WebSocket socket, string data, CancellationToken ct = default(CancellationToken))
		{
			byte[] buffer = Encoding.UTF8.GetBytes(data);
			ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
			return socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
		}

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
	}
	}
