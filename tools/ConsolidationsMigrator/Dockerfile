FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ./tools/ConsolidationsMigrator/ConsolidationsMigrator.csproj ./
COPY ./src/ConsolidationsApi/ConsolidationsApi.csproj ./ConsolidationsApi/
RUN dotnet restore "ConsolidationsMigrator.csproj"

# Copy source code
COPY ./tools/ConsolidationsMigrator .
COPY ./src/ConsolidationsApi/Data ./ConsolidationsApi/Data
COPY ./src/ConsolidationsApi/Models ./ConsolidationsApi/Models
COPY ./src/ConsolidationsApi/Migrations ./ConsolidationsApi/Migrations
RUN dotnet build "ConsolidationsMigrator.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "ConsolidationsMigrator.csproj" -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsolidationsMigrator.dll"]