# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /src

# Copy project files for layer caching
COPY ["src/DamControlSystem/DamControlSystem.csproj", "src/DamControlSystem/"]
RUN dotnet restore "src/DamControlSystem/DamControlSystem.csproj"

# Copy full source and publish
COPY . .
WORKDIR /src/src/DamControlSystem
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published binaries and wwwroot assets from build stage
COPY --from=builder /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DamControlSystem.dll"]
