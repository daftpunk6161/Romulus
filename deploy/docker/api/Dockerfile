FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore (layer cache)
COPY ["src/Romulus.Api/Romulus.Api.csproj", "src/Romulus.Api/"]
COPY ["src/Romulus.CLI/Romulus.CLI.csproj", "src/Romulus.CLI/"]
COPY ["src/Romulus.Contracts/Romulus.Contracts.csproj", "src/Romulus.Contracts/"]
COPY ["src/Romulus.Core/Romulus.Core.csproj", "src/Romulus.Core/"]
COPY ["src/Romulus.Infrastructure/Romulus.Infrastructure.csproj", "src/Romulus.Infrastructure/"]
RUN dotnet restore "src/Romulus.Api/Romulus.Api.csproj" \
    && dotnet restore "src/Romulus.CLI/Romulus.CLI.csproj"

COPY . .

# Publish API
RUN dotnet publish "src/Romulus.Api/Romulus.Api.csproj" -c Release -o /app/api /p:UseAppHost=false

# Publish CLI
RUN dotnet publish "src/Romulus.CLI/Romulus.CLI.csproj" -c Release -o /app/cli /p:UseAppHost=false

# ── Runtime ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/api/ ./api/
COPY --from=build /app/cli/ ./cli/
COPY data/ /app/data/

RUN mkdir -p /app/.romulus \
    && touch /app/.portable \
    && ln -s /app/cli/romulus.dll /usr/local/bin/romulus-cli

ENV ROMULUS_DATA_DIR=/app/data
EXPOSE 7878

USER app

# Default: start the API server. Override with CLI via:
#   docker run romulus romulus-cli health --console snes
ENTRYPOINT ["dotnet", "api/Romulus.Api.dll"]
