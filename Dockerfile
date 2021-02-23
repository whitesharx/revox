ARG DOTNET_VERSION=3.1-focal
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS sdk

WORKDIR /revox-build
COPY Revox/ /revox-build
RUN dotnet publish Revox.sln -c Release -o revox

FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS runtime

RUN apt-get update && apt-get install -y \
  firefox \
  firefox-geckodriver \
  && rm -rf /var/lib/apt/lists/*

COPY --from=sdk /revox-build/ /revox
ENTRYPOINT ["/revox/Revox"]
