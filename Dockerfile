#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.19 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.19 AS build
WORKDIR /src
COPY ["logindirector/logindirector.csproj", "."]
RUN dotnet restore "./logindirector.csproj"
COPY logindirector .
WORKDIR "/src/."
RUN dotnet build "logindirector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "logindirector.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
RUN apk update && apk upgrade && apk add curl && rm -rf /var/cache/apk
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "logindirector.dll"]
