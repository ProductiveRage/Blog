# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Avoid headaches trying to access the pre-calculated embedding data, and the original blog post content files
USER root 

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SemanticSearchDemo/SemanticSearchDemo.csproj", "SemanticSearchDemo/"]
RUN dotnet restore "./SemanticSearchDemo/SemanticSearchDemo.csproj"
COPY . .
WORKDIR "/src/SemanticSearchDemo"
RUN dotnet build "./SemanticSearchDemo.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SemanticSearchDemo.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SemanticSearchDemo.dll"]