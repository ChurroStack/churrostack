FROM python:3.13-slim
ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID
ARG TARGETARCH=x86_64
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV XDG_DATA_HOME=/app/home/.local
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        build-essential \
        dumb-init \
        git \
        git-lfs \
        htop \
        gcc \
        make \
        curl \
        libxcb1 \
        libx11-6 \
        libxext6 \
        libxrender1 \
        libsm6 \
        libglib2.0-0 \
        locales \
        lsb-release \
        man-db \
        nano \
        openssh-client \
        procps \
        sudo \
        vim-tiny \
        wget \
        zsh \
    && update-ca-certificates \
    && git lfs install \
    && rm -rf /var/lib/apt/lists/*

RUN sed -i "s/# en_US.UTF-8/en_US.UTF-8/" /etc/locale.gen \
    && locale-gen
ENV LANG=en_US.UTF-8

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app

RUN usermod -a -G dialout appuser

RUN python -m pip install altair pandas streamlit numpy fastapi uvicorn pydantic python-dotenv requests

RUN curl -fsSL https://code-server.dev/install.sh | sh

RUN code-server --install-extension Continue.continue
RUN code-server --install-extension ms-python.python

COPY --chown=$USER_UID:$USER_GID init.sh /app/init.sh
RUN chown -R $USER_UID:$USER_GID /app/home
RUN chmod +x /app/init.sh

# Expose default ports
EXPOSE 8000
EXPOSE 8501

USER $USER_UID:$USER_GID

ENTRYPOINT ["/app/init.sh"]