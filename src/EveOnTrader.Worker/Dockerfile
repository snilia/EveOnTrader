FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY ["src/EveOnTrader.Core/EveOnTrader.Core.csproj", "src/EveOnTrader.Core/"]
COPY ["src/EveOnTrader.Infra/EveOnTrader.Infra.csproj", "src/EveOnTrader.Infra/"]
COPY ["src/EveOnTrader.Worker/EveOnTrader.Worker.csproj", "src/EveOnTrader.Worker/"]

RUN dotnet restore "src/EveOnTrader.Worker/EveOnTrader.Worker.csproj"

COPY . .

WORKDIR "/src/src/EveOnTrader.Worker"

RUN dotnet publish "EveOnTrader.Worker.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "EveOnTrader.Worker.dll"]