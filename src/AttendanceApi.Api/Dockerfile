# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/AttendanceApi.Domain/AttendanceApi.Domain.csproj src/AttendanceApi.Domain/
COPY src/AttendanceApi.Application/AttendanceApi.Application.csproj src/AttendanceApi.Application/
COPY src/AttendanceApi.Infrastructure/AttendanceApi.Infrastructure.csproj src/AttendanceApi.Infrastructure/
COPY src/AttendanceApi.Api/AttendanceApi.Api.csproj src/AttendanceApi.Api/
RUN dotnet restore src/AttendanceApi.Api/AttendanceApi.Api.csproj

COPY src/ src/
RUN dotnet publish src/AttendanceApi.Api/AttendanceApi.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Serilog's rolling file sink writes to /app/logs; the non-root app user must own it.
RUN mkdir -p /app/logs && chown app:app /app/logs
USER app
ENTRYPOINT ["dotnet", "AttendanceApi.Api.dll"]
