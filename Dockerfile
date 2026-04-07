FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TankDesigner.sln ./
COPY TankDesigner.Core/TankDesigner.Core.csproj TankDesigner.Core/
COPY TankDesigner.Infrastructure/TankDesigner.Infrastructure.csproj TankDesigner.Infrastructure/
COPY TankDesigner.Web/TankDesigner.Web.csproj TankDesigner.Web/

RUN dotnet restore TankDesigner.Web/TankDesigner.Web.csproj

COPY . .

RUN dotnet publish TankDesigner.Web/TankDesigner.Web.csproj -c Release -o /app/publish

# Instalo Playwright/Chromium en una ruta fija
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN mkdir -p /ms-playwright
RUN /app/publish/playwright.sh install --with-deps chromium

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish .
COPY --from=build /ms-playwright /ms-playwright

EXPOSE 8080

ENTRYPOINT ["dotnet", "TankDesigner.Web.dll"]