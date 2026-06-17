# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY ["src/SemanticMemory.Api/SemanticMemory.Api.csproj", "src/SemanticMemory.Api/"]
COPY ["src/SemanticMemory.Application/SemanticMemory.Application.csproj", "src/SemanticMemory.Application/"]
COPY ["src/SemanticMemory.Domain/SemanticMemory.Domain.csproj", "src/SemanticMemory.Domain/"]
COPY ["src/SemanticMemory.Infrastructure/SemanticMemory.Infrastructure.csproj", "src/SemanticMemory.Infrastructure/"]

RUN dotnet restore "src/SemanticMemory.Api/SemanticMemory.Api.csproj"

FROM restore AS publish
COPY . .
RUN dotnet publish "src/SemanticMemory.Api/SemanticMemory.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "SemanticMemory.Api.dll"]
