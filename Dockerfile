FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY . .
COPY libpathfinder2.so /usr/lib/libpathfinder2.so
RUN dotnet restore
RUN dotnet publish -c Release -o /circles-nethermind-plugin

FROM nethermind/nethermind:1.21.1 AS base
COPY --from=build /circles-nethermind-plugin/libpathfinder2.so /usr/lib/libpathfinder2.so
COPY --from=build /circles-nethermind-plugin/Microsoft.Data.Sqlite.dll /usr/lib/
COPY --from=build /circles-nethermind-plugin/Nethermind.Int256.dll /usr/lib/
COPY --from=build /circles-nethermind-plugin/SQLitePCLRaw.batteries_v2.dll /usr/lib/
COPY --from=build /circles-nethermind-plugin/SQLitePCLRaw.core.dll /usr/lib/
COPY --from=build /circles-nethermind-plugin/SQLitePCLRaw.provider.e_sqlite3.dll /usr/lib/

WORKDIR /nethermind
COPY --from=build /circles-nethermind-plugin/Circles.Index.dll ./plugins
