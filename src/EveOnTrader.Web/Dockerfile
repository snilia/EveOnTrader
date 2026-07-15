FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY ["src/EveOnTrader.Core/EveOnTrader.Core.csproj", "src/EveOnTrader.Core/"]
COPY ["src/EveOnTrader.Infra/EveOnTrader.Infra.csproj", "src/EveOnTrader.Infra/"]
COPY ["src/EveOnTrader.Web/EveOnTrader.Web.csproj", "src/EveOnTrader.Web/"]

RUN dotnet restore "src/EveOnTrader.Web/EveOnTrader.Web.csproj"

COPY . .

WORKDIR "/src/src/EveOnTrader.Web"

RUN dotnet publish "EveOnTrader.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .
COPY ["deploy/aws/aws-rds-global-bundle.pem", "/app/certs/aws-rds-global-bundle.pem"]

USER $APP_UID

ENTRYPOINT ["dotnet", "EveOnTrader.Web.dll"]