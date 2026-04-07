using Domain.Messages;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

public interface IMessagePublisher
{
    Task PublishBookingRequestAsync(BookingRequestedEvent bookingEvent);
}

public class RabbitMQPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private const string QueueName = "booking-requests";

    public RabbitMQPublisher(string connectionString)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare queue — creates it if it doesn't exist
        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,      // survives RabbitMQ restart
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    public Task PublishBookingRequestAsync(BookingRequestedEvent bookingEvent)
    {
        var message = JsonSerializer.Serialize(bookingEvent);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true; // message survives restart

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: QueueName,
            basicProperties: properties,
            body: body);

        Console.WriteLine($"Published booking request for seat {bookingEvent.SeatId}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}