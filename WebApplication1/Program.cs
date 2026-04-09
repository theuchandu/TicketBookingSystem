using Application.Commands.BookSeat;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Database Configuration ---
// Pulls from Docker Environment Variable if available, otherwise defaults to local for dev
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=sql-db;Database=TicketBookingDb;User Id=sa;Password=P@ssw0rd2026!;TrustServerCertificate=True";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- 2. Redis Configuration ---
var redisConnectionString = builder.Configuration["Redis__ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false"));

 
// --- 3. RabbitMQ Configuration ---
var rabbitHost = builder.Configuration["RabbitMQ__HostName"] ?? "message-queue";
builder.Services.AddSingleton<IMessagePublisher>(new RabbitMQPublisher(rabbitHost));



// --- 4. Identity & JWT Authentication ---
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtKey = "YourSuperSecretKey_MustBeLong_123!"; // Ensure this matches AddJwtBearer below
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "your-app",
        ValidAudience = "your-app",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// --- 5. Background Services & MediatR ---
builder.Services.AddHostedService<BookingWorker>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<BookSeatHandler>();
});

// --- 6. Controllers & Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "Ticket Booking API", Version = "v1" });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[]{}
        }
    });
});

var app = builder.Build();

// --- 7. Middleware Pipeline ---
// Always enable Swagger in this dev environment so we can test in Docker
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();