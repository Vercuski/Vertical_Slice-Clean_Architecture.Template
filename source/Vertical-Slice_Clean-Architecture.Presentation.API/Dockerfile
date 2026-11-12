#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
#
# Build from the source/ folder, which is the build context (it holds Directory.Build.props and every project):
#   docker build source -f source/Vertical-Slice_Clean-Architecture.Presentation.API/Dockerfile --build-arg APP_VERSION=2.0.0 -t api:local
#   docker run --rm -p 8080:8080 api:local

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
# The ASP.NET Core image listens on HTTP port 8080. 8081 is the HTTPS port that Visual Studio's container tooling uses for debugging.
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
# Semantic version to stamp on the assemblies. The build context has no git history, so MinVer
# can't calculate the version from tags here; CI passes it in with
#   docker build --build-arg APP_VERSION=1.4.0 ...
# MSBuild reads environment variables as properties, so this becomes MinVerVersionOverride for
# both the build and publish stages. If it's omitted, MinVer falls back to
# {MinVerMinimumMajorMinor}.0-alpha.0 (2.0.0-alpha.0); see source/Directory.Build.props.
ARG APP_VERSION=
ENV MinVerVersionOverride=${APP_VERSION}
WORKDIR /src
# Copy only the project files first so the restore layer stays cached until a dependency changes.
COPY ["Directory.Build.props", "./"]
COPY ["Vertical-Slice_Clean-Architecture.Presentation.API/Vertical-Slice_Clean-Architecture.Presentation.API.csproj", "Vertical-Slice_Clean-Architecture.Presentation.API/"]
COPY ["Vertical-Slice_Clean-Architecture.Infrastructure/Vertical-Slice_Clean-Architecture.Infrastructure.csproj", "Vertical-Slice_Clean-Architecture.Infrastructure/"]
COPY ["Vertical-Slice_Clean-Architecture.Domain/Vertical-Slice_Clean-Architecture.Domain.csproj", "Vertical-Slice_Clean-Architecture.Domain/"]
RUN dotnet restore "./Vertical-Slice_Clean-Architecture.Presentation.API/Vertical-Slice_Clean-Architecture.Presentation.API.csproj"
COPY . .
WORKDIR "/src/Vertical-Slice_Clean-Architecture.Presentation.API"
RUN dotnet build "./Vertical-Slice_Clean-Architecture.Presentation.API.csproj" -c "$BUILD_CONFIGURATION" -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Vertical-Slice_Clean-Architecture.Presentation.API.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false

FROM base AS final
ARG APP_VERSION=
LABEL org.opencontainers.image.version=${APP_VERSION}
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Vertical-Slice_Clean-Architecture.Presentation.API.dll"]
