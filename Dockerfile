# Shared multi-stage build for every service: docker build --build-arg PROJECT=<path to .csproj>
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/TelecomPlatform.Contracts/*.csproj src/BuildingBlocks/TelecomPlatform.Contracts/
COPY src/BuildingBlocks/TelecomPlatform.Messaging/*.csproj src/BuildingBlocks/TelecomPlatform.Messaging/
COPY src/Services/Subscriber/Subscriber.Api/*.csproj src/Services/Subscriber/Subscriber.Api/
COPY src/Services/Billing/Billing.Api/*.csproj src/Services/Billing/Billing.Api/
COPY src/Services/Notification/Notification.Api/*.csproj src/Services/Notification/Notification.Api/
COPY src/Gateway/Gateway.Api/*.csproj src/Gateway/Gateway.Api/
RUN dotnet restore "$PROJECT"

COPY src/ src/
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ARG ASSEMBLY
ENV ASSEMBLY=${ASSEMBLY}
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ASSEMBLY.dll\""]
