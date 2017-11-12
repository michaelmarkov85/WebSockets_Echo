using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSockets_Echo
{
	public interface IWebsocketManager
	{
		bool AddSocket(WebSocket socket, string recipient);
		Task BroadcastAsync(string data, List<string> recipients, List<WebSocket> exceptSockets = null, CancellationToken ct = default(CancellationToken));
		Task BroadcastToAllAsync(string data, List<string> exceptRecipients = null, List<WebSocket> exceptSockets = null, CancellationToken ct = default(CancellationToken));
		string GetSocketOwner(WebSocket socket);
		List<WebSocket> GetSockets(string recipient);
		bool KillSocket(WebSocket socket);
		bool RemoveSocket(WebSocket socket);
		Task SendAsync(string data, string recipient, CancellationToken ct = default(CancellationToken));
		Task SendAsync(string data, WebSocket socket, CancellationToken ct = default(CancellationToken));
	}
}