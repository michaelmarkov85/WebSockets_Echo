using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSockets_Echo
{
	public class WebsocketManager
	{
		// key: [Owner] -> value:[SocketId collection] for adding and iteration
		private ConcurrentDictionary<string, List<string>> _owners = new ConcurrentDictionary<string, List<string>>();

		// key: [SocketId] -> value:[Owner] auxiliary dictionary for safe removal purpose only
		private ConcurrentDictionary<string, string> _ownersRemove = new ConcurrentDictionary<string, string>();

		// key: [SocketId] -> value:[WebSocket] for adding and iteration
		private ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

		// key: [WebSocket] -> value:[SocketId] auxiliary dictionary for safe removal purpose only
		private ConcurrentDictionary<WebSocket, string> _socketsRemove = new ConcurrentDictionary<WebSocket, string>();


		public bool AddSocket(WebSocket socket, string owner)
		{
			// Check if somehow socket is already in collection
			if (_socketsRemove.TryGetValue(socket, out string existingSocketId))
			{
				Console.WriteLine($"Warning! [AddSocket] Trying to add existing socket to a collection. Owner: {owner}, socketId: {existingSocketId}.");
				// if _ownersRemove contains record of that socket has an owner
				if (_ownersRemove.TryGetValue(existingSocketId, out string existingOwner))
				{
					// if right socket owner - just return existingSocketId 
					if (string.Equals(owner, existingOwner, StringComparison.InvariantCultureIgnoreCase))
						return true;
					else
						throw new Exception($"Error! [AddSocket] One socket belongs to different owners. " +
							$"SocketId: {existingSocketId}, existing Owner: {existingOwner}, new Owner: {owner}.");
				}
				else
					throw new Exception($"Error! [AddSocket] One socket record exists, but doesn't belongs to any owner. " +
							$"SocketId: {existingSocketId}, new Owner: {owner}.");
			}

			// Adding to socket collection
			string socketId = Guid.NewGuid().ToString();
			if (!_sockets.TryAdd(socketId, socket) || !_socketsRemove.TryAdd(socket, socketId))
				throw new Exception($"Error! [AddSocket] Cannot add new socket to a collection. " +
							$"SocketId: {socketId}, new Owner: {owner}.");

			// Adding owner's collection - to existing or a new one
			if (_owners.TryGetValue(owner, out List<string> socketIds))
			{
				try
				{
					socketIds.Add(socketId);
				}
				catch (Exception ex)
				{
					throw new Exception($"Error! [AddSocket] Cannot add new socketId to an existing owner. " +
							$"SocketId: {socketId}, new Owner: {owner}. Exception: {ex.Message}.");
				}
				if (!_ownersRemove.TryAdd(socketId, owner))
				{
					throw new Exception($"Error! [AddSocket] Cannot add new record to _ownersRemove collection. " +
							$"SocketId: {socketId}, new Owner: {owner}.");
				}
			}
			else
			{
				if(!_owners.TryAdd(owner, new List<string> { socketId }) || !_ownersRemove.TryAdd(socketId, owner))
					throw new Exception($"Error! [AddSocket] Cannot add new record to _owners or _ownersRemove collection. " +
							$"SocketId: {socketId}, new Owner: {owner}.");
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

		public string GetSocketId(WebSocket socket)
		{
			bool result = _socketsRemove.TryGetValue(socket, out string socketId);
			return result ? socketId : null;
		}
		public string GetSocketOwner(WebSocket socket)
		{
			string owner = null;
			string socketId = GetSocketId(socket);
			if (!string.IsNullOrEmpty(socketId))
				_ownersRemove.TryGetValue(socketId, out owner);
			return owner;
		}
		public List<WebSocket> GetSockets(string owner)
		{
			List<WebSocket> sockets = new List<WebSocket>();
			if (_owners.TryGetValue(owner, out List<string> socketIds))
				if (socketIds?.Count > 0)
					foreach (var id in socketIds)
						if (_sockets.TryGetValue(id, out WebSocket s))
							if (s!=null && s.State == WebSocketState.Open)
								sockets.Add(s);
			return sockets;
		}


		public async Task BroadcastMessage(string recipient, string data, CancellationToken ct = default(CancellationToken))
		{
			throw new NotImplementedException();
		}

		public async Task SendStringAsync(WebSocket socket, string data, CancellationToken ct = default(CancellationToken))
		{
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data);
			ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
			await socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
		}

		/// <summary>
		/// Gets collection of owner's sockets.
		/// </summary>
		/// <param name="ownerId">Owner's Id.</param>
		/// <returns>Collection of sockets in Open state.</returns>
		internal IEnumerable<WebSocket> GetSocketsByOwner(string ownerId)
		{
			if (_owners.TryGetValue(ownerId, out List<string> socketIds))
				if (socketIds != null && socketIds.Count > 0)
					foreach (string sId in socketIds)
						if (_sockets.TryGetValue(sId, out WebSocket socket))
							if (socket != null || socket.State == WebSocketState.Open)
								yield return socket;
		}

		internal async Task SendToRecipientAsync(string recipient, string data, CancellationToken ct = default(CancellationToken))
		{
			if (string.IsNullOrEmpty(recipient) || string.IsNullOrEmpty(data))
			{
				Console.WriteLine($"[SendToRecipientAsync] Invalid arguments: recipient:{recipient}, data:{data}");
				return;
			}

			IEnumerable<WebSocket> sockets = GetSocketsByOwner(recipient);
			List<Task> tasks = new List<Task>();
			if (sockets == null || sockets.Count() < 1)
				return;

			foreach (var s in sockets)
			{
				tasks.Add(Task.Run(async () => await SendStringAsync(s, data, ct)));
			}
			await Task.WhenAll(tasks);
		}
	}
}
