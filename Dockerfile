# Multi-stage Dockerfile for Fenix Legal OS (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY FenixLegalOs.csproj ./
RUN dotnet restore FenixLegalOs.csproj

# Copy the rest of the application files
COPY . ./

# Publish release build
RUN dotnet publish FenixLegalOs.csproj -c Release -o /app/publish --no-restore

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published files
COPY --from=build /app/publish ./

# Create directory for persistent SQLite database
ENV FENIX_DB_PATH=/data/fenix.db
ENV PORT=5050
RUN mkdir -p /data

EXPOSE 5050

ENTRYPOINT ["dotnet", "FenixLegalOs.dll"]
