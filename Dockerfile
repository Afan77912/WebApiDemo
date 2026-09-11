FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WebApiDemo.csproj", "./"]

RUN dotnet restore "WebApiDemo.csproj"

COPY . .

RUN dotnet build "WebApiDemo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebApiDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "WebApiDemo.dll"]