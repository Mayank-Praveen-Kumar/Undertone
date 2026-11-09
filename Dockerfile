# undertone-server: headless Undertone (OpenSubsonic API + library scanner + device sync).
#
#   docker build -t undertone-server .
#   docker run -d --name undertone -p 4747:4747 \
#     -v /path/to/music:/music:ro -v undertone-data:/data undertone-server
#   docker exec -it undertone /app/undertone-server user add alice
#
# The container speaks plain HTTP on 4747 (UNDERTONE_TLS=0): terminate TLS in your
# reverse proxy. Set UNDERTONE_TLS=1 to have it use its own self-signed certificate
# instead (the fingerprint is printed at start; pin it in the client).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY src/Undertone.Core/Undertone.Core.csproj src/Undertone.Core/
COPY src/Undertone.Core.Server/Undertone.Core.Server.csproj src/Undertone.Core.Server/
COPY src/Undertone.Server/Undertone.Server.csproj src/Undertone.Server/
RUN dotnet restore src/Undertone.Server/Undertone.Server.csproj -a "$TARGETARCH"
COPY src/Undertone.Core/ src/Undertone.Core/
COPY src/Undertone.Core.Server/ src/Undertone.Core.Server/
COPY src/Undertone.Server/ src/Undertone.Server/
RUN dotnet publish src/Undertone.Server/Undertone.Server.csproj -c Release -a "$TARGETARCH" \
      --no-restore --self-contained false -p:PublishReadyToRun=false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV UNDERTONE_DATA_DIR=/data \
    UNDERTONE_MUSIC=/music \
    UNDERTONE_PORT=4747 \
    UNDERTONE_TLS=0 \
    DOTNET_gcServer=0
VOLUME ["/data", "/music"]
EXPOSE 4747
ENTRYPOINT ["/app/undertone-server"]
CMD ["serve"]
