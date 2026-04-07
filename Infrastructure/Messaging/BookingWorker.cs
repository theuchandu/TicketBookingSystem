using Domain.Entities;
using Domain.Messages;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

public class BookingWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private const string QueueName = "booking-requests";

    public BookingWorker(
        IServiceProvider services,
        ILogger<BookingWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672")
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        // Only process one message at a time
        _channel.BasicQos(0, 1, false);

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var bookingEvent = JsonSerializer
                    .Deserialize<BookingRequestedEvent>(message);

                if (bookingEvent != null)
                    await ProcessBookingAsync(bookingEvent);

                // Acknowledge — tell RabbitMQ we processed it
                _channel.BasicAck(ea.DeliveryTag, false);
                _logger.LogInformation(
                    "Processed booking for seat {SeatId}",
                    bookingEvent?.SeatId);
            }
            catch (Exception ex)
            {
                // Reject — put back in queue to retry
                _channel.BasicNack(ea.DeliveryTag, false, true);
                _logger.LogError(ex, "Failed to process booking");
            }
        };

        _channel.BasicConsume(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessBookingAsync(BookingRequestedEvent bookingEvent)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var seat = await context.Seats.FindAsync(bookingEvent.SeatId);

        if (seat == null || seat.Status != SeatStatus.Available)
        {
            _logger.LogWarning(
                "Seat {SeatId} not available", bookingEvent.SeatId);
            return;
        }

        seat.Status = SeatStatus.Booked;

        context.Bookings.Add(new Booking
        {
            SeatId = seat.Id,
            UserId = bookingEvent.UserId,
            BookedAt = DateTime.UtcNow,
            Status = BookingStatus.Confirmed
        });

        await context.SaveChangesAsync();
        _logger.LogInformation(
            "Booking confirmed for seat {SeatId} by {UserId}",
            bookingEvent.SeatId, bookingEvent.UserId);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}