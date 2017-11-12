using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WebSockets_Echo.RmqListener
{
	public class RmqClient
	{
		private readonly RmqMessageHandler _handler;
		const string _queueName = "my-send-receive-queue";

		public RmqClient(RmqMessageHandler handler)
		{
			_handler = handler;
		}

		public void Start()
		{
			Console.WriteLine("starting consumption");
			var factory = new ConnectionFactory()
			{
				HostName = "localhost",
				Port = 5672,
				UserName = "guest",
				Password = "guest"
			};
			var connection = factory.CreateConnection();
			var channel = connection.CreateModel();
				channel.QueueDeclare(queue: _queueName,
										durable: true,
										exclusive: false,
										autoDelete: false,
										arguments: null);
				var consumer = new EventingBasicConsumer(channel);
				consumer.Received += async (model, ea) => { await FireOnNewMessage(model, ea); };
				channel.BasicConsume(queue: _queueName,
										autoAck: true,
										consumer: consumer);
				Console.WriteLine("Done.");
		}
		private async Task FireOnNewMessage(object model, BasicDeliverEventArgs ea)
		{
			var body = ea.Body;
			string message = Encoding.UTF8.GetString(body);
			await _handler.ProcessMessage(message);
		}
	}
}
