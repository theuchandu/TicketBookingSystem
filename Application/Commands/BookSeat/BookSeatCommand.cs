using Domain.Entities;
using Domain.Messages;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Application.Commands.BookSeat;

public class BookSeatCommand : IRequest<BookSeatResult>
{
    public int SeatId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class BookSeatResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BookSeatHandler : IRequestHandler<BookSeatCommand, BookSeatResult>
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionMultiplexer _redis;
    private readonly IMessagePublisher _publisher;

    public BookSeatHandler(
        ApplicationDbContext context,
        IConnectionMultiplexer redis,
        IMessagePublisher publisher)
    {
        _context = context;
        _redis = redis;
        _publisher = publisher;
    }

    public async Task<BookSeatResult> Handle(
        BookSeatCommand request,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        // Layer 1 — Redis lock
        var lockKey = $"seat-lock:{request.SeatId}";
        var lockValue = Guid.NewGuid().ToString();

        bool lockAcquired = await db.StringSetAsync(
            lockKey, lockValue,
            TimeSpan.FromSeconds(10),
            When.NotExists);

        if (!lockAcquired)
            return new BookSeatResult
            {
                Success = false,
                Message = "Seat is being booked. Please try again!"
            };

        try
        {
            // Layer 2 — Check availability
            var seat = await _context.Seats
                .FirstOrDefaultAsync(s => s.Id == request.SeatId,
                    cancellationToken);

            if (seat == null)
                return new BookSeatResult
                {
                    Success = false,
                    Message = "Seat not found"
                };

            if (seat.Status != SeatStatus.Available)
                return new BookSeatResult
                {
                    Success = false,
                    Message = "Seat is already booked!"
                };

            // Layer 3 — Publish to RabbitMQ queue
            await _publisher.PublishBookingRequestAsync(
                new BookingRequestedEvent
                {
                    SeatId = request.SeatId,
                    UserId = request.UserId,
                    RequestedAt = DateTime.UtcNow
                });

            // Instantly return — worker processes in background
            return new BookSeatResult
            {
                Success = true,
                Message = $"Booking request received! Seat {seat.SeatNumber} is being confirmed."
            };
        }
        finally
        {
            // Always release Redis lock
            var currentValue = await db.StringGetAsync(lockKey);
            if (currentValue == lockValue)
                await db.KeyDeleteAsync(lockKey);
        }
    }
}