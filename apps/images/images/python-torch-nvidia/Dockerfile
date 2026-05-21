FROM pytorch/pytorch:2.10.0-cuda12.6-cudnn9-runtime
ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID
ARG TARGETARCH=x86_64
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        build-essential \
        ca-certificates \
        ffmpeg \
        gcc \
        make \
        curl \
        libxcb1 \
        libx11-6 \
        libxext6 \
        libxrender1 \
        libsm6 \
        libglib2.0-0 \
        python3.12-venv \
    && rm -rf /var/lib/apt/lists/* \
    && update-ca-certificates

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app

RUN usermod -a -G dialout appuser

COPY --chown=$USER_UID:$USER_GID ./ttyd.${TARGETARCH} /usr/bin/ttyd
RUN chmod +x /usr/bin/ttyd

COPY --chown=$USER_UID:$USER_GID init.sh /app/init.sh
RUN chmod +x /app/init.sh

# Expose default ports
EXPOSE 3001
EXPOSE 8000
EXPOSE 8501

USER $USER_UID:$USER_GID

# Allow passing arguments directly to gotty
ENTRYPOINT ["/app/init.sh"]