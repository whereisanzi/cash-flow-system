FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ./tools/TransactionsMigrator/TransactionsMigrator.csproj ./
RUN dotnet restore "TransactionsMigrator.csproj"

# Copy source code and migrations
COPY ./tools/TransactionsMigrator .
COPY ./src/TransactionsApi/Migrations ./Migrations
RUN dotnet build "TransactionsMigrator.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "TransactionsMigrator.csproj" -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransactionsMigrator.dll"]