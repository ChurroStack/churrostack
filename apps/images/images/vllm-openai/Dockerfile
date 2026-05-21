FROM vllm/vllm-openai:latest


RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
    && update-ca-certificates

ARG USER_UID=999
ARG USER_GID=$USER_UID

WORKDIR /app

RUN groupadd -r -g $USER_UID appuser \
    && useradd -r -u $USER_UID -g $USER_GID -d /app -s /usr/sbin/nologin appuser \
    && chown -R $USER_UID:$USER_GID /app

RUN mkdir -p /app/home && chown -R $USER_UID:$USER_GID /app

# Fix ownership and permissions of the nv_internal directory
RUN chown -R $USER_UID:0 /usr/local/lib/python3.12/dist-packages/flashinfer/data/csrc/nv_internal && \
    chmod -R g+rwX /usr/local/lib/python3.12/dist-packages/flashinfer/data/csrc/nv_internal && \
    find /usr/local/lib/python3.12/dist-packages/flashinfer/data/csrc/nv_internal -type d -exec chmod g+ws {} \;

EXPOSE 8001
USER $USER_UID:$USER_GID