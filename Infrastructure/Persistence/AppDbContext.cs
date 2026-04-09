using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence; // <--- Make sure '.Persistence' is here

// We change 'DbContext' to 'IdentityDbContext'
// This tells EF Core to include tables for Users, Roles, and Claims.
public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Your existing DbSets (Seats, Bookings, etc.) stay here
    
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Booking> Bookings { get; set; } //

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // THIS LINE IS CRITICAL:
        base.OnModelCreating(builder);

        // Your existing RowVersion configuration
        builder.Entity<Seat>().Property(s => s.RowVersion).IsRowVersion();
    }
}