# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Copy project files to their respective folders
COPY ["WebApplication1/API.csproj", "WebApplication1/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]

# 2. Restore dependencies
RUN dotnet restore "WebApplication1/API.csproj"

# 3. Copy the rest of the source code
COPY . .

# 4. Build the API
WORKDIR "/src/WebApplication1"
RUN dotnet build "API.csproj" -c Release -o /app/build

# 5. Publish the API
FROM build AS publish
RUN dotnet publish "API.csproj" -c Release -o /app/publish

# 6. Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]