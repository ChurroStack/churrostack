FROM alpine

ARG TARGETARCH=ttyd.x86_64
ARG USER_UID=999
ARG USER_GID=$USER_UID

RUN apk add --no-cache bash tini

COPY ./${TARGETARCH} /usr/bin/ttyd

RUN mkdir -p /app/home && chown -R $USER_UID:$USER_GID /app && chown -R $USER_UID:$USER_GID /usr/bin/ttyd && chmod +x /usr/bin/ttyd

EXPOSE 3001
USER $USER_UID:$USER_GID

WORKDIR /app/home

ENTRYPOINT ["/sbin/tini", "--"]
CMD ["ttyd", "-W", "bash"]