# BoulderTime API.
#
# Kept at the repository root on purpose: hosting platforms look here first and build it without being told,
# which is one fewer setting that can be lost or ignored. Build context is the root:
#   docker build .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/BoulderTime.Api/BoulderTime.Api.csproj -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
# The image's non-root user; the API needs no privileges.
USER app
EXPOSE 8080
# Hosting platforms pass the port in $PORT.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet BoulderTime.Api.dll"]
