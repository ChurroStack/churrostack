docker build --platform linux/amd64 -t quay.io/churrostack/churros-api:latest -f ./ChurrOS.Api/Dockerfile .
docker push quay.io/churrostack/churros-api:latest