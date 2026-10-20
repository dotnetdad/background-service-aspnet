FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Shop/Shop.csproj src/Shop/
RUN dotnet restore src/Shop/Shop.csproj
COPY src/ src/
RUN dotnet publish src/Shop/Shop.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Exec-форма: сигнал остановки получает сам .NET, а не оболочка.
ENTRYPOINT ["dotnet", "Shop.dll"]
