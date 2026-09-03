# Build context is this directory (CollarSystem.Relay/) - see fly.toml.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CollarSystem.Relay.csproj .
RUN dotnet restore CollarSystem.Relay.csproj

COPY . .
RUN dotnet publish CollarSystem.Relay.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CollarSystem.Relay.dll"]
