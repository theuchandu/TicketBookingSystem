namespace Domain.Messages;

public class BookingRequestedEvent
{
    public int SeatId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
}