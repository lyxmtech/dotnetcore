FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BusCore/BusCore.csproj BusCore/
COPY DbCore/DbCore.csproj DbCore/
COPY DataCore/DataCore.csproj DataCore/
COPY HelloService/HelloService.csproj HelloService/
RUN dotnet restore HelloService/HelloService.csproj
COPY . .
RUN dotnet publish HelloService/HelloService.csproj -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# The SQLite file lives on a volume so it survives container restarts.
VOLUME ["/data"]
ENV Database__Path=/data/hello.db
EXPOSE 80
ENTRYPOINT ["dotnet", "HelloService.dll"]
