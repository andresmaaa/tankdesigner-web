FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TankDesigner.sln ./
COPY TankDesigner.Core/TankDesigner.Core.csproj TankDesigner.Core/
COPY TankDesigner.Infrastructure/TankDesigner.Infrastructure.csproj TankDesigner.Infrastructure/
COPY TankDesigner.Web/TankDesigner.Web.csproj TankDesigner.Web/

RUN dotnet restore TankDesigner.Web/TankDesigner.Web.csproj

COPY . .

RUN dotnet publish TankDesigner.Web/TankDesigner.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TankDesigner.Web.dll"]