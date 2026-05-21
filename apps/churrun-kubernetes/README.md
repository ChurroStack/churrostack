# churrun-kubernetes
Runner for Kubernetes

## Deploy an instance
```
POST /api/deployments
Content-Type: application/json

{
  "name": "streamlit-x11-app",
  "template": "com.churrostack.streamlit-x11/<version>",
  "replicas": 1,
  "size": {
    "hint": "1x2gb",
    "cpu": "1",
    "memory": "2Gi",
    "storage": "10Gi",
    "gpu": null
  },
  "parameters": { "path": "/app/home", "entry": "/code/main.py" },
  "extensions": [
    { "name": "console", "template": "com.churrostack.extension.console/<version>", "parameters": { "path": "/app/home" } },
    { "name": "file-browser", "template": "com.churrostack.extension.file-browser/<version>", "parameters": { "path": "/app/home" } },
    { "name": "storage", "template": "com.churrostack.extension.storage/<version>", "parameters": { "path": "/app/home", "size": "1Gi" } }
  ]
}
```
