# CI's build job publishes the app for each architecture into the build context's amd64/, arm64/ and arm/, which
# Buildx picks with TARGETARCH. Visual Studio's container debugging builds only the base stage
ARG legacy_image_warning=""

FROM mcr.microsoft.com/dotnet/runtime:11.0@sha256:3fe5f5495141c97986a49d3a4e76061f6225c3f93f94e8a7e522781376193863 AS base
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
