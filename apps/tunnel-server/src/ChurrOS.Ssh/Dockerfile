FROM alpine:3.19

RUN apk add --no-cache \
    postgresql-client \
    openssh \
    tini \
    libcap \
    ca-certificates \
    tzdata

RUN mkdir -p /var/run/sshd

RUN adduser -D -H -s /sbin/nologin tunnel \
    && passwd -u tunnel
    
COPY ./sshd_config /sshd_config
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
COPY ./reverse_tunnel_key_lookup.sh /usr/local/bin/reverse_tunnel_key_lookup.sh
RUN chmod +x /usr/local/bin/reverse_tunnel_key_lookup.sh

RUN touch /run/authorized_keys_env && \
    chmod 600 /run/authorized_keys_env && \
    chown tunnel:tunnel /run/authorized_keys_env

RUN setcap \
    'cap_net_bind_service=+ep cap_setuid=+ep cap_setgid=+ep' \
    /usr/sbin/sshd

EXPOSE 8443

ENTRYPOINT ["/sbin/tini", "--", "/entrypoint.sh"]
CMD ["/usr/sbin/sshd", "-D", "-e"]