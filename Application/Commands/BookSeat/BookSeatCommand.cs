using Domain.Entities;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

    public BookSeatHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BookSeatResult> Handle(
        BookSeatCommand request,
        CancellationToken cancellationToken)
    {
        var seat = await _context.Seats
            .FirstOrDefaultAsync(s => s.Id == request.SeatId,
                cancellationToken);

        if (seat == null)
            return new BookSeatResult { Success = false, Message = "Seat not found" };

        if (seat.Status != SeatStatus.Available)
            return new BookSeatResult { Success = false, Message = "Seat already booked!" };

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
}