# CI's build job publishes the app for each architecture into the build context's amd64/, arm64/ and arm/, which
# Buildx picks with TARGETARCH. Visual Studio's container debugging builds only the base stage
ARG legacy_image_warning=""

FROM mcr.microsoft.com/dotnet/runtime:11.0@sha256:43a34aaf5edd4ae1a36dd82fe857602e4b5449bfcf1a9bed753eeb0727dc0c70 AS base
ARG legacy_image_warning
ENV TESLAMATE_LEGACY_IMAGE_WARNING=${legacy_image_warning}
USER app
WORKDIR /app

FROM base AS final
ARG TARGETARCH
LABEL org.opencontainers.image.source="https://github.com/AmyJeanes/TeslaMateAgile" \
      org.opencontainers.image.licenses="MIT"
COPY ${TARGETARCH}/ .
ENTRYPOINT ["dotnet", "TeslaMateAgile.dll"]
