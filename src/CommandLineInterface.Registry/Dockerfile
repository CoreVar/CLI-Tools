FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CommandLineInterface.Registry/CommandLineInterface.Registry.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 COREVAR_REGISTRY_DATA=/data
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "CoreVar.CommandLineInterface.Registry.dll"]
