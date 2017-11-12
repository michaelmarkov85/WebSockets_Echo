using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSockets_Echo
{
	public class WebsocketManager : IWebsocketManager
	{
		#region Socket and Owner Collections
		// key: [Owner] -> value:[SocketId collection] for adding and iteration
		private ConcurrentDictionary<string, List<string>> _owners = new ConcurrentDictionary<string, List<string>>();

		// key: [SocketId] -> value:[Owner] auxiliary dictionary for safe removal purpose only
		private ConcurrentDictionary<string, string> _ownersRemove = new ConcurrentDictionary<string, string>();

		// key: [SocketId] -> value:[WebSocket] for adding and iteration
		private ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

		// key: [WebSocket] -> value:[SocketId] auxiliary dictionary for safe removal purpose only
		private ConcurrentDictionary<WebSocket, string> _socketsRemove = new ConcurrentDictionary<WebSocket, string>();
		#endregion

		#region Socket Management
		// Interface
		public bool AddSocket(WebSocket socket, string recipient)
		{
			// Check if somehow socket is already in collection
			if (_socketsRemove.TryGetValue(socket, out string existingSocketId))
			{
				Console.WriteLine($"Warning! [AddSocket] Trying to add existing socket to a collection. Owner: {recipient}, socketId: {existingSocketId}.");
				// if _ownersRemove contains record of that socket has an owner
				if (_ownersRemove.TryGetValue(existingSocketId, out string existingOwner))
				{
					// if right socket owner - just return existingSocketId 
					if (string.Equals(recipient, existingOwner, StringComparison.InvariantCultureIgnoreCase))
						return true;
					else
						throw new Exception($"Error! [AddSocket] One socket belongs to different owners. " +
							$"SocketId: {existingSocketId}, existing Owner: {existingOwner}, new Owner: {recipient}.");
				}
				else
					throw new Exception($"Error! [AddSocket] One socket record exists, but doesn't belongs to any owner. " +
							$"SocketId: {existingSocketId}, new Owner: {recipient}.");
			}

			// Adding to socket collection
			string socketId = Guid.NewGuid().ToString();
			if (!_sockets.TryAdd(socketId, socket) || !_socketsRemove.TryAdd(socket, socketId))
				throw new Exception($"Error! [AddSocket] Cannot add new socket to a collection. " +
							$"SocketId: {socketId}, new Owner: {recipient}.");

			// Adding owner's collection - to existing or a new one
			if (_owners.TryGetValue(recipient, out List<string> socketIds))
			{
				try
				{
					socketIds.Add(socketId);
				}
				catch (Exception ex)
				{
					throw new Exception($"Error! [AddSocket] Cannot add new socketId to an existing owner. " +
							$"SocketId: {socketId}, new Owner: {recipient}. Exception: {ex.Message}.");
				}
				if (!_ownersRemove.TryAdd(socketId, recipient))
				{
					throw new Exception($"Error! [AddSocket] Cannot add new record to _ownersRemove collection. " +
							$"SocketId: {socketId}, new Owner: {recipient}.");
				}
			}
			else
			{
				if (!_owners.TryAdd(recipient, new List<string> { socketId }) || !_ownersRemove.TryAdd(socketId, recipient))
					throw new Exception($"Error! [AddSocket] Cannot add new record to _owners or _ownersRemove collection. " +
							$"SocketId: {socketId}, new Owner: {recipient}.");
			}

			return true;
		}
		public bool RemoveSocket(WebSocket socket)
		{
			if (_socketsRemove.TryRemove(socket, out string socketId))
				if (_sockets.TryRemove(socketId, out WebSocket existingSocket))
					if (_ownersRemove.TryRemove(socketId, out string owner))
						if (_owners.TryGetValue(owner, out var bag))
							if (bag.Remove(socketId))
								return true;
			return false;
		}
		public bool KillSocket(WebSocket socket)
		{
			// TODO: Implement safe socket closing.
			return RemoveSocket(socket);
		}
		public string GetSocketOwner(WebSocket socket)
		{
			string owner = null;
			string socketId = GetSocketId(socket);
			if (!string.IsNullOrEmpty(socketId))
				_ownersRemove.TryGetValue(socketId, out owner);
			return owner;
		}
		public List<WebSocket> GetSockets(string recipient)
		{
			List<WebSocket> sockets = new List<WebSocket>();
			if (_owners.TryGetValue(recipient, out List<string> socketIds))
				if (socketIds?.Count > 0)
					foreach (var id in socketIds)
						if (_sockets.TryGetValue(id, out WebSocket s))
							if (s != null && s.State == WebSocketState.Open)
								sockets.Add(s);
			return sockets;
		}
		// Private
		private string GetSocketId(WebSocket socket)
		{
			bool result = _socketsRemove.TryGetValue(socket, out string socketId);
			return result ? socketId : null;
		}
		#endregion

		#region Sending and Broadcasting messages
		// Interface
		public async Task SendAsync(string data, string recipient, CancellationToken ct = default(CancellationToken))
		{
			if (string.IsNullOrEmpty(recipient) || string.IsNullOrEmpty(data))
			{
				Console.WriteLine($"[SendToRecipientAsync] Invalid arguments: recipient:{recipient}, data:{data}");
				return;
			}

			List<WebSocket> sockets = GetSocketsByRecipient(recipient).ToList();
			if (sockets == null || sockets.Count() < 1)
				return;

			await SendToMultipleSocketsAsync(data, sockets, ct);
		}
		public async Task SendAsync(string data, WebSocket socket, CancellationToken ct = default(CancellationToken))
		{
			if (socket == null || socket.State != WebSocketState.Open)
			{
				Console.WriteLine($"[WebsocketManager.SendAsync] WebSocket is null or not opened.");
				return;
			}
			if (string.IsNullOrWhiteSpace(data))
			{
				Console.WriteLine($"[WebsocketManager.SendAsync] Message data is null or whitespace.");
				return;
			}

			if (ct.IsCancellationRequested)
				return;

			await SendToSocketAsync(data, socket, ct);
		}
		public async Task BroadcastAsync(string data, List<string> recipients, List<WebSocket> exceptSockets = null, CancellationToken ct = default(CancellationToken))
		{
			if (recipients == null || recipients.Count < 1)
			{
				Console.WriteLine($"[WebsocketManager.BroadcastAsync] Recipients are null or none.");
				return;
			}

			List<WebSocket> sockets = GetSocketsByRecipients(recipients).ToList();

			// Excluding forbidden sockets
			if (exceptSockets != null && exceptSockets.Count > 0)
				sockets = sockets.Except(exceptSockets).ToList();

			await SendToMultipleSocketsAsync(data, sockets, ct);
		}
		public async Task BroadcastToAllAsync(string data, List<string> exceptRecipients = null, List<WebSocket> exceptSockets = null, CancellationToken ct = default(CancellationToken))
		{
			// Getting recipients
			List<string> recipients = _owners.Keys.ToList();
			if (recipients == null || recipients.Count < 1)
			{
				Console.WriteLine($"[WebsocketManager.BroadcastToAllAsync] Recipients are null or none.");
				return;
			}
			// Excluding forbidden recipients
			if (exceptRecipients != null && exceptRecipients.Count > 0)
			{
				recipients = recipients.Except(exceptRecipients).ToList();
				// Check if anyone left
				if (recipients == null || recipients.Count < 1)
					return;
			}

			// Getting sockets
			List<WebSocket> sockets = GetSocketsByRecipients(recipients).ToList();
			if (recipients == null || recipients.Count < 1)
			{
				Console.WriteLine($"[WebsocketManager.BroadcastToAllAsync] There are no sockets for requested recipients.");
				return;
			}
			// Excluding forbidden sockets
			if (exceptSockets != null && exceptSockets.Count > 0)
			{
				sockets = sockets.Except(exceptSockets).ToList();
				// Check if any left
				if (sockets == null || sockets.Count < 1)
					return;
			}

			await SendToMultipleSocketsAsync(data, sockets, ct);
		}
		// Private
		private IEnumerable<WebSocket> GetSocketsByRecipient(string recipient)
		{
			if (_owners.TryGetValue(recipient, out List<string> socketIds))
				if (socketIds != null && socketIds.Count > 0)
					foreach (string sId in socketIds)
						if (_sockets.TryGetValue(sId, out WebSocket socket))
							if (socket != null || socket.State == WebSocketState.Open)
								yield return socket;
		}
		private IEnumerable<WebSocket> GetSocketsByRecipients(List<string> recipients)
		{
			List<WebSocket> result = new List<WebSocket>();
			if (recipients == null || recipients.Count < 1)
				return result;

			foreach (var r in recipients)
			{
				var portion = GetSocketsByRecipient(r);
				if (portion != null && portion.Count() > 0)
					result.AddRange(portion.ToList());
			}

			return result;
		}
		private async Task SendToMultipleSocketsAsync(string data, List<WebSocket> sockets, CancellationToken ct)
		{
			List<Task> tasks = new List<Task>();
			foreach (var s in sockets)
			{
				if (ct.IsCancellationRequested)
					break;
				tasks.Add(Task.Run(async () => await SendToSocketAsync(data, s, ct)));
			}
			await Task.WhenAll(tasks);
		}
		private async Task SendToSocketAsync(string data, WebSocket socket, CancellationToken ct = default(CancellationToken))
		{
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data);
			ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
			await socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
		}

		#endregion




		//public async Task SendToRecipientAsync(string recipient, string data, CancellationToken ct = default(CancellationToken))
		//{
		//	if (string.IsNullOrEmpty(recipient) || string.IsNullOrEmpty(data))
		//	{
		//		Console.WriteLine($"[SendToRecipientAsync] Invalid arguments: recipient:{recipient}, data:{data}");
		//		return;
		//	}

		//	IEnumerable<WebSocket> sockets = GetSocketsByRecipient(recipient);
		//	if (sockets == null || sockets.Count() < 1)
		//		return;

		//	List<Task> tasks = new List<Task>();
		//	foreach (var s in sockets)
		//	{
		//		tasks.Add(Task.Run(async () => await SendToSocketAsync(data, s, ct)));
		//	}
		//	await Task.WhenAll(tasks);
		//}

		//public async Task BroadcastMessageToAll(string message, CancellationToken ct = default(CancellationToken))
		//{
		//	List<Task> tasks = new List<Task>();
		//	foreach (var owner in _owners.Keys)
		//	{
		//		tasks.Add(Task.Run(async () => await BroadcastMessage(owner, message, ct)));
		//	}
		//	await Task.WhenAll(tasks);
		//}
	}
}
