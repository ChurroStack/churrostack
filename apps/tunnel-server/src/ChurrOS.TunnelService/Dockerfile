FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0
WORKDIR /src
COPY ["ChurrOS.TunnelService/ChurrOS.TunnelService.csproj", "ChurrOS.TunnelService/"]
RUN dotnet restore "./ChurrOS.TunnelService/ChurrOS.TunnelService.csproj"
COPY . .
WORKDIR "/src/ChurrOS.TunnelService"
RUN dotnet build "./ChurrOS.TunnelService.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:Version=$VERSION

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0
RUN dotnet publish "./ChurrOS.TunnelService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChurrOS.TunnelService.dll"]