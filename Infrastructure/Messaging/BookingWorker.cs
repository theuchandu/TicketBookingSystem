using Domain.Entities;
using Domain.Messages;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; // Added for IConfiguration
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

public class BookingWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingWorker> _logger;
    private readonly IConfiguration _configuration; // Added Configuration injection
    private IConnection? _connection;
    private IModel? _channel;
    private const string QueueName = "booking-requests";

    public BookingWorker(
        IServiceProvider services,
        ILogger<BookingWorker> logger,
        IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitHost = _configuration["RabbitMQ__HostName"] ?? "message-queue";
        var factory = new ConnectionFactory() { HostName = rabbitHost };

        // 1. Connection Retry Loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);

                _logger.LogInformation("[Success] BookingWorker connected to RabbitMQ at {Host}", rabbitHost);
                break; // Exit loop on success
            }
            catch (Exception)
            {
                _logger.LogWarning("[Retry] BookingWorker waiting for RabbitMQ at {Host}...", rabbitHost);
                await Task.Delay(5000, stoppingToken); // Wait 5 seconds and try again
            }
        }

        // 2. Consumer Logic
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var bookingEvent = JsonSerializer.Deserialize<BookingRequestedEvent>(message);
                if (bookingEvent != null)
                {
                    await ProcessBookingAsync(bookingEvent);
                }

                // Acknowledge the message (remove from queue)
                //_channel.BasicAck(ea.DeliveryTag, false);
                // Add the ! after _channel
                _channel!.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing booking message");
                // Reject and requeue message on failure
                //_channel.BasicNack(ea.DeliveryTag, false, true);
                // Add the ! after _channel
                _channel!.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        // Keep the task alive until the application stops
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(BookingRequestedEvent bookingEvent)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var seat = await context.Seats.FindAsync(bookingEvent.SeatId);

        if (seat == null || seat.Status != SeatStatus.Available)
        {
            _logger.LogWarning("Seat {SeatId} not available for User {UserId}", bookingEvent.SeatId, bookingEvent.UserId);
            return;
        }

        // Update Seat Status
        seat.Status = SeatStatus.Booked;

        // Create Booking Record
        context.Bookings.Add(new Booking
        {
            SeatId = seat.Id,
            UserId = bookingEvent.UserId,
            BookedAt = DateTime.UtcNow,
            Status = BookingStatus.Confirmed
        });

        await context.SaveChangesAsync();
        _logger.LogInformation("[Confirmed] Seat {SeatId} booked by {UserId}", bookingEvent.SeatId, bookingEvent.UserId);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}