FROM mirror.gcr.io/library/debian:13-slim
ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app

# OpenBox & Firefox
RUN apt-get update -y && apt-get install -y --no-install-recommends \
    wget \
    curl \
    unzip \
    gnupg \
    supervisor \
    curl \
    ca-certificates \
    xterm \
    xvfb \
    x11vnc \
    openbox \
    dbus-x11 \
    novnc \
    websockify \
    firefox-esr 

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

COPY --chown=$USER_UID:$USER_GID console.sh /app/console.sh
RUN chmod +x /app/console.sh
COPY --chown=$USER_UID:$USER_GID supervisord.conf /etc/supervisor/conf.d/supervisord.conf

RUN cp /usr/share/novnc/vnc_lite.html /usr/share/novnc/index.html
RUN sed -i 's/rfb.scaleViewport = readQueryVariable.*$/rfb.scaleViewport = true;/' /usr/share/novnc/index.html
EXPOSE 3001

# File Browser
RUN curl -fsSL https://raw.githubusercontent.com/filebrowser/get/master/get.sh | bash
COPY --chown=$USER_UID:$USER_GID file-browser.sh /app/file-browser.sh
RUN chmod +x /app/file-browser.sh
EXPOSE 3002

# Console (termnal)
RUN wget -O gotty.tar.gz https://github.com/sorenisanerd/gotty/releases/download/v1.6.0/gotty_v1.6.0_linux_amd64.tar.gz && \
    tar -xzf gotty.tar.gz -C /app --strip-components=1 && \
    rm /app/gotty.tar.gz
COPY --chown=$USER_UID:$USER_GID terminal.sh /app/terminal.sh
RUN chmod +x /app/gotty
RUN chmod +x /app/terminal.sh
EXPOSE 3003

# Create home
RUN mkdir -p /app/home \
    && chown -R $USER_UID:$USER_GID /app

USER $USER_UID:$USER_GID

CMD ["/usr/bin/supervisord", "-n", "-c", "/etc/supervisor/conf.d/supervisord.conf"]