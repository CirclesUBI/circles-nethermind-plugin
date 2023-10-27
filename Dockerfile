FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY . .
COPY libpathfinder2.so /usr/lib/libpathfinder2.so
RUN dotnet publish -c Release -o /circles-nethermind-plugin

FROM nethermind/nethermind:latest AS base
WORKDIR /nethermind
COPY --from=build /circles-nethermind-plugin ./plugins
