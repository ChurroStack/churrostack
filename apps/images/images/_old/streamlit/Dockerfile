FROM churrostack/base:latest

ENV DEBIAN_FRONTEND=noninteractive
ARG USER_UID=999
ARG USER_GID=$USER_UID

# stops Python from writing pyc files and buffering stdin/stdout
ENV PYTHONWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

USER root

RUN apt-get update && apt-get install -y \
    build-essential \
    curl \
    git \
    python3 \
    python3-full \
    python3-venv \
    python3-pip \
    python-is-python3

RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

RUN python -m pip install altair pandas streamlit numpy

COPY --chown=$USER_UID:$USER_GID app.sh /app/app.sh
COPY --chown=$USER_UID:$USER_GID streamlit_app.py /app/home/code/streamlit_app.py
RUN chmod +x /app/app.sh

COPY --chown=$USER_UID:$USER_GID supervisord.conf /app/supervisord.conf
RUN cat /app/supervisord.conf >> /etc/supervisor/conf.d/supervisord.conf
RUN rm /app/supervisord.conf

USER $USER_UID:$USER_GID

EXPOSE 8501

# test if container is working. 1 is for unhealthy status, so exit if unhealthy.
HEALTHCHECK CMD ["curl", "--fail", "http://localhost:8501/_stcore/health", "||", "exit", "1"]