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

    public RabbitMQPublisher(string hostname)
    {
        var factory = new ConnectionFactory() { HostName = hostname };

        int retryCount = 0;
        while (true)
        {
            try
            {
                // Attempt to establish connection
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare the queue as durable so messages survive a RabbitMQ restart
                _channel.QueueDeclare(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                Console.WriteLine($"[Success] Connected to RabbitMQ at {hostname}");
                break; // Exit loop on success
            }
            catch (Exception)
            {
                retryCount++;
                if (retryCount > 10)
                {
                    throw new Exception($"Failed to connect to RabbitMQ at {hostname} after 10 attempts.");
                }

                Console.WriteLine($"[Retry] RabbitMQ at {hostname} not ready. Retrying in 5s... (Attempt {retryCount}/10)");
                Thread.Sleep(5000); // Wait 5 seconds before next attempt
            }
        }
    }

    public Task PublishBookingRequestAsync(BookingRequestedEvent bookingEvent)
    {
        var message = JsonSerializer.Serialize(bookingEvent);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true; // Mark message as persistent

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: QueueName,
            basicProperties: properties,
            body: body);

        Console.WriteLine($"[Sent] Published booking request for Seat ID: {bookingEvent.SeatId}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Cleanup resources properly
        if (_channel is { IsOpen: true }) _channel.Close();
        if (_connection is { IsOpen: true }) _connection.Close();
    }
}