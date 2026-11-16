# Build context: repo root.  docker build -f src/DevWeave.Workers/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props DevWeave.sln ./
COPY src/ src/
RUN dotnet publish src/DevWeave.Workers/DevWeave.Workers.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "DevWeave.Workers.dll"]
