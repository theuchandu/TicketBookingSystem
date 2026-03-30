namespace Domain.Entities;

public class Seat
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Event Event { get; set; } = null!;
}

public enum SeatStatus
{
    Available,
    Locked,
    Booked
}