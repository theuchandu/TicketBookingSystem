using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Concurrency control — prevents double booking!
        modelBuilder.Entity<Seat>()
            .Property(s => s.RowVersion)
            .IsRowVersion();

        // Seed test data
        modelBuilder.Entity<Event>().HasData(new Event
        {
            Id = 1,
            Name = "IPL Finals 2025",
            Venue = "Hyderabad Stadium",
            EventDate = DateTime.UtcNow.AddDays(30)
        });

        // Seed 10 seats for the event
        for (int i = 1; i <= 10; i++)
        {
            modelBuilder.Entity<Seat>().HasData(new Seat
            {
                Id = i,
                EventId = 1,
                SeatNumber = $"A{i}",
                Status = SeatStatus.Available
            });
        }
    }
}