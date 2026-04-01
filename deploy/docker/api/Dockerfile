FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RomCleanup.Api/RomCleanup.Api.csproj", "src/RomCleanup.Api/"]
COPY ["src/RomCleanup.Contracts/RomCleanup.Contracts.csproj", "src/RomCleanup.Contracts/"]
COPY ["src/RomCleanup.Core/RomCleanup.Core.csproj", "src/RomCleanup.Core/"]
COPY ["src/RomCleanup.Infrastructure/RomCleanup.Infrastructure.csproj", "src/RomCleanup.Infrastructure/"]
RUN dotnet restore "src/RomCleanup.Api/RomCleanup.Api.csproj"

COPY . .
RUN dotnet publish "src/RomCleanup.Api/RomCleanup.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish/ ./
COPY data/ /app/data/

RUN mkdir -p /app/.romcleanup \
    && touch /app/.portable

ENV ROMCLEANUP_DATA_DIR=/app/data
EXPOSE 7878

ENTRYPOINT ["dotnet", "RomCleanup.Api.dll"]
