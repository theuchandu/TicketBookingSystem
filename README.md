# Ticket Booking System

A distributed ticket booking system built with .NET 8 Clean Architecture.

## Tech Stack
- ASP.NET Core Web API
- Entity Framework Core + SQL Server
- Redis (distributed locking)
- RabbitMQ (async processing)
- MediatR (CQRS pattern)
- Docker

## Architecture
- Domain — Entities (Seat, Event, Booking)
- Application — CQRS Commands & Queries
- Infrastructure — EF Core, Redis, RabbitMQ
- API — Controllers, Swagger

## Features
- Seat booking with concurrency control
- Prevention of double booking
- Queue-based booking flow
