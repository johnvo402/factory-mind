FROM golang:1.24.13-bookworm AS source
ENV CGO_ENABLED=0
WORKDIR /src

FROM source AS server-build
RUN git clone --depth 1 --branch RELEASE.2025-09-07T16-13-09Z https://github.com/minio/minio.git . \
    && test "$(git rev-parse HEAD)" = 07c3a429bfed433e49018cb0f78a52145d4bedeb \
    && go build -trimpath -ldflags "$(go run buildscripts/gen-ldflags.go)" -o /out/minio .

FROM source AS client-build
RUN git clone --depth 1 --branch RELEASE.2025-08-13T08-35-41Z https://github.com/minio/mc.git . \
    && test "$(git rev-parse HEAD)" = 7394ce0dd2a80935aded936b09fa12cbb3cb8096 \
    && go build -trimpath -ldflags "$(go run buildscripts/gen-ldflags.go)" -o /out/mc .

FROM debian:bookworm-slim AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

FROM runtime AS client
COPY --from=client-build /src/LICENSE /usr/share/doc/minio-mc/LICENSE
COPY --from=client-build /out/mc /usr/local/bin/mc
ENTRYPOINT ["mc"]

FROM runtime AS server
COPY --from=server-build /src/LICENSE /usr/share/doc/minio/LICENSE
COPY --from=server-build /out/minio /usr/local/bin/minio
EXPOSE 9000 9001
ENTRYPOINT ["minio"]
CMD ["server", "/data", "--console-address", ":9001"]
