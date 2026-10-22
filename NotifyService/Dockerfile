FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BusCore/BusCore.csproj BusCore/
COPY NotifyService/NotifyService.csproj NotifyService/
RUN dotnet restore NotifyService/NotifyService.csproj
COPY . .
RUN dotnet publish NotifyService/NotifyService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotifyService.dll"]
