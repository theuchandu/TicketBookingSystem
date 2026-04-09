using Application.Commands.BookSeat;
using Application.Queries.GetSeats;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; 

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SeatsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeatsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("available/{eventId}")]
    public async Task<IActionResult> GetAvailableSeats(int eventId)
    {
        var result = await _mediator.Send(
            new GetAvailableSeatsQuery { EventId = eventId });
        return Ok(result);
    }

    [HttpPost("book")]
    public async Task<IActionResult> BookSeat([FromBody] BookSeatRequest request)
    {
        var result = await _mediator.Send(new BookSeatCommand
        {
            SeatId = request.SeatId,
            UserId = request.UserId
        });

        if (result.Success)
            return Ok(result);

        return BadRequest(result);
    }
}

public class BookSeatRequest
{
    public int SeatId { get; set; }
    public string UserId { get; set; } = string.Empty;
}