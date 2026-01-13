ARG arch=noble
ARG version="1.0.0"
ARG informational_version="1.0.0"
ARG legacy_image_warning=""

FROM mcr.microsoft.com/dotnet/runtime:10.0-${arch} AS base
ARG legacy_image_warning
ENV TESLAMATE_LEGACY_IMAGE_WARNING=${legacy_image_warning}
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:5504edd1267dd4deab3443f960cfab219249c8bd935fbcc358f1c24aeae23fe0 AS build
ARG BUILD_CONFIGURATION=Release
ARG version
ARG informational_version
WORKDIR /src
COPY ["TeslaMateAgile/TeslaMateAgile.csproj", "TeslaMateAgile/"]
RUN dotnet restore "./TeslaMateAgile/TeslaMateAgile.csproj"
COPY . .
WORKDIR "/src/TeslaMateAgile"
RUN dotnet build "./TeslaMateAgile.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    -p:Version=$version \
    -p:InformationalVersion="$informational_version"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG version
ARG informational_version
RUN dotnet publish "./TeslaMateAgile.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:Version=$version \
    /p:InformationalVersion="$informational_version"

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TeslaMateAgile.dll"]
