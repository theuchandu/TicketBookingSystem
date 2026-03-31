using Domain.Entities;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.GetSeats;

public class GetAvailableSeatsQuery : IRequest<List<SeatDto>>
{
    public int EventId { get; set; }
}

public class SeatDto
{
    public int Id { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class GetAvailableSeatsHandler
    : IRequestHandler<GetAvailableSeatsQuery, List<SeatDto>>
{
    private readonly AppDbContext _context;

    public GetAvailableSeatsHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SeatDto>> Handle(
        GetAvailableSeatsQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Seats
            .Where(s => s.EventId == request.EventId
                     && s.Status == SeatStatus.Available)
            .Select(s => new SeatDto
            {
                Id = s.Id,
                SeatNumber = s.SeatNumber,
                Status = s.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }
}