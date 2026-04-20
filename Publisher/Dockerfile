# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# IMPORTANT: Copy Directory.Packages.props FIRST before any project files
COPY ["Directory.Packages.props", "./"]

COPY ["Common/Cosmos.Common.csproj", "Common/"]
COPY ["AspNetCore.Identity.FlexDb/AspNetCore.Identity.FlexDb.csproj", "AspNetCore.Identity.FlexDb/"]
COPY ["Cosmos.BlobService/Cosmos.BlobService.csproj", "Cosmos.BlobService/"]
COPY ["Cosmos.ConnectionStrings/Cosmos.DynamicConfig.csproj", "Cosmos.ConnectionStrings/"]
COPY ["Cosmos.EmailServices/Cosmos.EmailServices.csproj", "Cosmos.EmailServices/"]
COPY ["Cosmos.MicrosoftGraph/Cosmos.MicrosoftGraph.csproj", "Cosmos.MicrosoftGraph/"]
COPY ["Sky.Shared.Razor/Sky.Shared.Razor.csproj", "Sky.Shared.Razor/"]
COPY ["Publisher/Sky.Publisher.csproj", "Publisher/"]
RUN dotnet restore "./Publisher/Sky.Publisher.csproj"
COPY . .
WORKDIR "/src/Publisher"

RUN dotnet build "./Sky.Publisher.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:GeneratePackageOnBuild=false /p:IncludeSymbols=false

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Sky.Publisher.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false /p:IncludeSymbols=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sky.Publisher.dll"]
