using Application.Commands.BookSeat;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TicketBookingDB;Integrated Security=True;TrustServerCertificate=True;"));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

// RabbitMQ Publisher
builder.Services.AddSingleton<IMessagePublisher>(
    new RabbitMQPublisher("amqp://guest:guest@localhost:5672"));

// Background Worker — listens to queue
builder.Services.AddHostedService<BookingWorker>();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<BookSeatHandler>();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();