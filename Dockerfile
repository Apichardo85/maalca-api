FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Maalca.sln .
COPY src/Maalca.Domain/Maalca.Domain.csproj src/Maalca.Domain/
COPY src/Maalca.Application/Maalca.Application.csproj src/Maalca.Application/
COPY src/Maalca.Infrastructure/Maalca.Infrastructure.csproj src/Maalca.Infrastructure/
COPY src/Maalca.Api/Maalca.Api.csproj src/Maalca.Api/

RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish src/Maalca.Api/Maalca.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Maalca.Api.dll"]
