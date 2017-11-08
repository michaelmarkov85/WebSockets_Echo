using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WebSockets_Echo
{
	public class Program
	{
		const string _queueName = "my-send-receive-queue";

		public static void Main(string[] args)
		{
			Console.WriteLine("starting consumption");
			var factory = new ConnectionFactory()
			{
				HostName = "localhost",
				Port = 5672,
				UserName = "guest",
				Password = "guest"
			};
			using (var connection = factory.CreateConnection())
			using (var channel = connection.CreateModel())
			{
				channel.QueueDeclare(queue: _queueName,
										durable: true,
										exclusive: false,
										autoDelete: false,
										arguments: null);
				var consumer = new EventingBasicConsumer(channel);
				consumer.Received += async (model, ea) => { await _FireOnNewMessage(model, ea); };
				channel.BasicConsume(queue: _queueName,
										autoAck: true,
										consumer: consumer);
				Console.WriteLine("Done.");

				BuildWebHost(args).Run();
			}

		}

		private static async Task _FireOnNewMessage(object model, BasicDeliverEventArgs ea)
		{
			var body = ea.Body;
			string message = Encoding.UTF8.GetString(body);
			await NotificationWsMiddleware.ProcessMessage(message);
		}

		public static IWebHost BuildWebHost(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
				.UseStartup<Startup>()
				.Build();

	}
}
