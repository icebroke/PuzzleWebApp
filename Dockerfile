FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PuzzleWebApp.csproj", "./"]
RUN dotnet restore "PuzzleWebApp.csproj"

COPY . .
RUN dotnet publish "PuzzleWebApp.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="Data Source=/data/puzzle.db"

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet PuzzleWebApp.dll --urls http://0.0.0.0:${PORT:-8080}"]