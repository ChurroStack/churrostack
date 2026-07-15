FROM debian:trixie-slim
ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV HOME=/app

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app

# Enable the "contrib" component so ttf-mscorefonts-installer is installable.
# Debian trixie ships deb822-format sources (/etc/apt/sources.list.d/debian.sources);
# fall back to appending to the legacy one-line sources.list if that's what's present.
RUN if [ -f /etc/apt/sources.list.d/debian.sources ]; then \
        sed -i 's/^Components: main$/Components: main contrib/' /etc/apt/sources.list.d/debian.sources; \
    fi \
    && if [ -f /etc/apt/sources.list ]; then \
        sed -i 's/^\(deb .*main\)$/\1 contrib/' /etc/apt/sources.list; \
    fi

# Auto-accept the msttcorefonts EULA so the mscorefonts-installer apt step is non-interactive.
RUN echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" \
        | debconf-set-selections

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        python3 \
        python3-venv \
        python3-pip \
        # LibreOffice: writer/calc/impress/draw cover doc(x)/xls(x)/ppt(x)/odf/rtf/csv.
        libreoffice-writer \
        libreoffice-calc \
        libreoffice-impress \
        libreoffice-draw \
        libreoffice-core \
        # Proprietary MS core fonts (Arial, Times New Roman, Courier New, Verdana...).
        ttf-mscorefonts-installer \
        # Metric-compatible Calibri/Cambria -- NOT included in mscorefonts.
        fonts-crosextra-carlito \
        fonts-crosextra-caladea \
        # Fallbacks / broad glyph coverage.
        fonts-liberation \
        fonts-noto-cjk \
        fonts-noto-color-emoji \
    && rm -rf /var/lib/apt/lists/* \
    && update-ca-certificates \
    && fc-cache -f

RUN python3 -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

RUN pip install --no-cache-dir fastapi "uvicorn[standard]" python-multipart

COPY --chown=$USER_UID:$USER_GID app /app/app
COPY --chown=$USER_UID:$USER_GID init.sh /app/init.sh
RUN chmod +x /app/init.sh

RUN mkdir -p /app/home/jobs && chown -R $USER_UID:$USER_GID /app

EXPOSE 8000

USER $USER_UID:$USER_GID

ENTRYPOINT ["/app/init.sh"]
