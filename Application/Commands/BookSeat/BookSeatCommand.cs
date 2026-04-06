using Domain.Entities;
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
    private readonly AppDbContext _context;
    private readonly IConnectionMultiplexer _redis;

    public BookSeatHandler(AppDbContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis;
    }

    public async Task<BookSeatResult> Handle(
        BookSeatCommand request,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        // Step 1 — Try to acquire Redis lock
        // Only one user can hold this lock at a time!
        var lockKey = $"seat-lock:{request.SeatId}";
        var lockValue = Guid.NewGuid().ToString();
        var lockExpiry = TimeSpan.FromSeconds(10);

        bool lockAcquired = await db.StringSetAsync(
            lockKey,
            lockValue,
            lockExpiry,
            When.NotExists); // Only set if key doesn't exist

        if (!lockAcquired)
        {
            return new BookSeatResult
            {
                Success = false,
                Message = "Seat is being booked by someone else. Please try again!"
            };
        }

        try
        {
            // Step 2 — Find the seat
            var seat = await _context.Seats
                .FirstOrDefaultAsync(s => s.Id == request.SeatId,
                    cancellationToken);

            if (seat == null)
                return new BookSeatResult
                {
                    Success = false,
                    Message = "Seat not found"
                };

            // Step 3 — Check availability
            if (seat.Status != SeatStatus.Available)
                return new BookSeatResult
                {
                    Success = false,
                    Message = "Seat is already booked!"
                };

            // Step 4 — Book the seat
            seat.Status = SeatStatus.Booked;

            var booking = new Booking
            {
                SeatId = seat.Id,
                UserId = request.UserId,
                BookedAt = DateTime.UtcNow,
                Status = BookingStatus.Confirmed
            };

            _context.Bookings.Add(booking);

            try
            {
                // Step 5 — Save with RowVersion as backup
                await _context.SaveChangesAsync(cancellationToken);
                return new BookSeatResult
                {
                    Success = true,
                    Message = "Seat " + seat.SeatNumber + " booked successfully!"
                };
            }
            catch (DbUpdateConcurrencyException)
            {
                return new BookSeatResult
                {
                    Success = false,
                    Message = "Seat was just taken. Please choose another!"
                };
            }
        }
        finally
        {
            // Step 6 — Always release the lock!
            var currentValue = await db.StringGetAsync(lockKey);
            if (currentValue == lockValue)
                await db.KeyDeleteAsync(lockKey);
        }
    }
}