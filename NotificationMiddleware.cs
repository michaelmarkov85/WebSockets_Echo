using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace WebSockets_Echo
{
	public class NotificationMiddleware
	{
		const string OWNER_QUERY_STRING_TOKEN_KEY = "owner";

		private readonly RequestDelegate _next;
		private readonly IdentityProvider _identity;
		private readonly WebsocketManager _wsm;
		private readonly ClientMessages.ClientMessageHandler _clientMessageHandler;


		public NotificationMiddleware(RequestDelegate next, WebsocketManager wsm)
		{
			_next = next;
			_identity = new IdentityProvider();
			_wsm = wsm;
			_clientMessageHandler = new ClientMessages.ClientMessageHandler(_wsm);
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
				_wsm.AddSocket(currentSocket, owner);
				await _clientMessageHandler.Listen(currentSocket, ct);
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

		public async Task ProcessMessage(string message, CancellationToken ct = default(CancellationToken))
		{
			var jsonSettings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };

			Msg msg = ParseMessage(message);

			if (!msg?.IsValid ?? true)
				return;

			switch (msg.Type.ToLower())
			{
				case "offer_commented_data":
					await ProcessRmqMessage(msg);
					break;
				default:
					Console.WriteLine($"Couldn't find match action for message type '{msg.Type}'");
					break;
			}
		}

		private async Task ProcessRmqMessage(Msg msg)
		{

			if(!msg?.IsValid ?? false)
			{
				Console.WriteLine($"Couldn't find match action for message type '{msg.Type}'");
				return;
			}

			string recipient = msg.Recipient;

			WebsocketMessage wsMessagt = msg;

			await _wsm.SendToRecipientAsync(recipient, (msg as WebsocketMessage).ToString());

		}

		


		private static Msg ParseMessage(string message)
		{
			Msg result = null;
			try
			{
				result = JsonConvert.DeserializeObject<Msg>(message );
			}
			catch (Exception ex)
			{
				var t = ex.Message;
			}
			return result;
		}


		
	}
	}
