FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BusCore/BusCore.csproj BusCore/
COPY DbCore/DbCore.csproj DbCore/
COPY DataCore/DataCore.csproj DataCore/
COPY DemoHost/DemoHost.csproj DemoHost/
RUN dotnet restore DemoHost/DemoHost.csproj
COPY . .
RUN dotnet publish DemoHost/DemoHost.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# The SQLite file lives on a volume so it survives container restarts.
VOLUME ["/data"]
ENV Database__Path=/data/demo.db
EXPOSE 80
ENTRYPOINT ["dotnet", "DemoHost.dll"]
