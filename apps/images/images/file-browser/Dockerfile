FROM debian:bookworm-slim
ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app


# Install minimal dependencies, download and install gotty
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
    && update-ca-certificates

RUN curl -fsSL https://raw.githubusercontent.com/filebrowser/get/master/get.sh | bash

# Create home
RUN mkdir -p /app/home && chown -R $USER_UID:$USER_GID /app

COPY --chown=$USER_UID:$USER_GID init.sh /app/init.sh
RUN chmod +x /app/init.sh

# Expose default GoTTY port
EXPOSE 3002

USER $USER_UID:$USER_GID

ENTRYPOINT ["/app/init.sh"]