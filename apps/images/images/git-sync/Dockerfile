FROM alpine:3.19

ARG USER_UID=999
ARG USER_GID=$USER_UID

WORKDIR /app

RUN apk add --no-cache \
  git \
  openssh \
  ca-certificates \
  bash \
  && update-ca-certificates

RUN adduser  -u ${USER_UID} -S appuser -h /app -s /sbin/nologin \
  && mkdir -p /app \
  && chown -R ${USER_UID}:${USER_GID} /app

COPY --chown=$USER_UID:$USER_GID entrypoint.sh .
RUN chmod +x /app/entrypoint.sh

USER $USER_UID:$USER_GID
RUN git config --global --add safe.directory '*'

ENTRYPOINT ["/app/entrypoint.sh"]

