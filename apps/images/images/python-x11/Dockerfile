FROM python:3.13-slim
ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID
ARG TARGETARCH=x86_64
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        build-essential \
        gcc \
        make \
        curl \
        libxcb1 \
        libx11-6 \
        libxext6 \
        libxrender1 \
        libsm6 \
        libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && update-ca-certificates

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app

RUN usermod -a -G dialout appuser

# OpenBox & Firefox
RUN apt-get update -y && apt-get install -y --no-install-recommends \
    build-essential \
    wget \
    git \
    curl \
    unzip \
    gnupg \
    ca-certificates \
    xterm \
    xvfb \
    x11vnc \
    openbox \
    x11-xserver-utils \
    dbus-x11 \
    novnc \
    websockify \
    python3 \
    python3-full \
    python3-venv \
    python3-pip \
    python-is-python3 \
    firefox-esr 

RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

RUN python -m pip install altair pandas streamlit==1.56.0 numpy fastapi uvicorn pydantic python-dotenv requests

# Google Chrome
RUN wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | \
    gpg --no-default-keyring --keyring gnupg-ring:/etc/apt/trusted.gpg.d/google.gpg --import; \
    chmod 644 /etc/apt/trusted.gpg.d/google.gpg; \
    echo "deb http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list

RUN curl -LO https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb && \
    apt-get install -y ./google-chrome-stable_current_amd64.deb && \
    rm google-chrome-stable_current_amd64.deb

RUN mv /usr/bin/google-chrome-stable /usr/bin/google-chrome.real
RUN rm /usr/bin/google-chrome
COPY --chown=$USER_UID:$USER_GID google-chrome /usr/bin/google-chrome-stable
RUN chmod +x /usr/bin/google-chrome-stable

COPY --chown=$USER_UID:$USER_GID ./ttyd.${TARGETARCH} /usr/bin/ttyd
RUN chmod +x /usr/bin/ttyd

COPY --chown=$USER_UID:$USER_GID init.sh /app/init.sh
RUN chmod +x /app/init.sh
RUN cp /usr/share/novnc/vnc_lite.html /usr/share/novnc/index.html
RUN sed -i 's/rfb.scaleViewport = readQueryVariable.*$/rfb.scaleViewport = true;/' /usr/share/novnc/index.html
RUN chown -R $USER_UID:$USER_GID /opt/venv

# Download the package to configure the Microsoft repo
RUN curl -sSL -O https://packages.microsoft.com/config/debian/$(grep VERSION_ID /etc/os-release | cut -d '"' -f 2 | cut -d '.' -f 1)/packages-microsoft-prod.deb
# Install the package
RUN dpkg -i packages-microsoft-prod.deb
# Delete the file
RUN rm packages-microsoft-prod.deb
RUN apt-get update
RUN ACCEPT_EULA=Y apt-get install -y msodbcsql18
# optional: for bcp and sqlcmd
RUN ACCEPT_EULA=Y apt-get install -y mssql-tools18
RUN echo 'export PATH="$PATH:/opt/mssql-tools18/bin"' >> ~/.bashrc
# optional: for unixODBC development headers
RUN apt-get install -y unixodbc-dev
# optional: kerberos library for debian-slim distributions
RUN apt-get install -y libgssapi-krb5-2

# Expose default ports
EXPOSE 3001
EXPOSE 3003
EXPOSE 8000
EXPOSE 8501

USER $USER_UID:$USER_GID

# Allow passing arguments directly to gotty
ENTRYPOINT ["/app/init.sh"]