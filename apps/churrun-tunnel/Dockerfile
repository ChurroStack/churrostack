FROM alpine:3.19

RUN apk add --no-cache \
    openssh-client \
    autossh \
    bash \
    ca-certificates \
    && adduser -D -u 10001 tunnel

USER tunnel
WORKDIR /home/tunnel

RUN mkdir -p .ssh && mkdir -p .ssh/keys && chown tunnel:tunnel .ssh && chmod 700 .ssh

ENV AUTOSSH_LOGFILE=/dev/stdout \
    AUTOSSH_GATETIME=0 \
    AUTOSSH_POLL=30 \
    AUTOSSH_FIRST_POLL=30 \
    SSH_PORT=22 \
    LOCAL_HOST=127.0.0.1

COPY --chown=tunnel ./entrypoint.sh /home/tunnel/entrypoint.sh
RUN chmod +x /home/tunnel/entrypoint.sh

ENTRYPOINT ["/home/tunnel/entrypoint.sh"]