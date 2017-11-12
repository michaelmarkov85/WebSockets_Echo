using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebSockets_Echo
{
	public class NotificationMiddleware
	{
		const string OWNER_QUERY_STRING_TOKEN_KEY = "owner";

		private readonly RequestDelegate _next;
		private readonly IdentityProvider _identity;
		private readonly IWebsocketManager _wsManager;
		private readonly WsListener.WsClient _wsListener;
		private readonly WsListener.WsMessageHandler _wsMessageHandler;

		public NotificationMiddleware(RequestDelegate next, IWebsocketManager wsManager)
		{
			_next = next;
			_identity = new IdentityProvider();
			_wsManager = wsManager;
			_wsMessageHandler = new WsListener.WsMessageHandler(_wsManager);
			_wsListener = new WsListener.WsClient(_wsMessageHandler, _wsManager);
		}

		public async Task Invoke(HttpContext context)
		{
			// If not WebSockets request - ignore this and go to next middleware
			if (!context.WebSockets.IsWebSocketRequest)
			{
				await _next.Invoke(context);
				return;
			}

			// Establishing WebSocket connection
			CancellationToken ct = context.RequestAborted;
			WebSocket currentSocket = await context.WebSockets.AcceptWebSocketAsync();

			if (currentSocket == null || currentSocket.State != WebSocketState.Open)
				return;

			if (!AreSubProtocolsSupported(currentSocket.SubProtocol))
				return;

			// Getting token from which determine a user
			string token = context.Request.Query[OWNER_QUERY_STRING_TOKEN_KEY].ToString();
			if (string.IsNullOrWhiteSpace(token))
				return;

			// Getting user/owner
			string owner = _identity.GetOwner(token);
			if (string.IsNullOrWhiteSpace(owner) || !Guid.TryParse(owner, out Guid g))
				return;


			try
			{
				_wsManager.AddSocket(currentSocket, owner);
				await _wsListener.Listen(currentSocket, ct);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		private bool AreSubProtocolsSupported(string subProtocol)
		{
			// TODO: Implement sub protocols
			return true;
		}
	}
}
