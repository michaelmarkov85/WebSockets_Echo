using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace WebSockets_Echo
{
	public class Startup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<IWebsocketManager, WebsocketManager >();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			app.UseWebSockets(new WebSocketOptions()
			{
				KeepAliveInterval = TimeSpan.FromSeconds(120),
				ReceiveBufferSize = 4 * 1024
			});

			// Registering middleware that manages WebSocket connections and 
			// listens to frontend client messages
			app.UseMiddleware<NotificationMiddleware>();

			app.UseFileServer();

			// Starting RmqClient that listens to backend queue messages
			app.Run((s) => Task.Run(() =>
			{
				var ws = app.ApplicationServices.GetService<IWebsocketManager>();
				var handler = new RmqListener.RmqMessageHandler(ws);
				var client = new RmqListener.RmqClient(handler);
				client.Start();
			}));
		}
	}
}
