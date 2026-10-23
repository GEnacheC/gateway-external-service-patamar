FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Patamar.Gateway.External.slnx ./
COPY src/Patamar.Gateway.External.Domain/Patamar.Gateway.External.Domain.csproj src/Patamar.Gateway.External.Domain/
COPY src/Patamar.Gateway.External.Application/Patamar.Gateway.External.Application.csproj src/Patamar.Gateway.External.Application/
COPY src/Patamar.Gateway.External.Infrastructure/Patamar.Gateway.External.Infrastructure.csproj src/Patamar.Gateway.External.Infrastructure/
COPY src/Patamar.Gateway.External.Api/Patamar.Gateway.External.Api.csproj src/Patamar.Gateway.External.Api/

RUN dotnet restore src/Patamar.Gateway.External.Api/Patamar.Gateway.External.Api.csproj

COPY src/ src/
RUN dotnet publish src/Patamar.Gateway.External.Api/Patamar.Gateway.External.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Patamar.Gateway.External.Api.dll"]
