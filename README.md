# ChurroStack

> Deploy apps like churros. \
> From code to production in minutes. \
> We fry deployments, not your brain.

Welcome to the Helm installation repository for ChurroStack. ChurroStack is a comprehensive open-source platform designed to operate and manage your on-premises infrastructure and private cloud servers. It provides you with the capability to deploy a wide variety of applications and tools, including Streamlit applications, Model Context Protocol (MCP) tools, Large Language Models (LLMs), and many other services, as simple as making churros ;).

## Features

- Your apps run on your own hardware, not in the cloud—100% private.
- Private servers or VPS can be behind a NAT, company firewall, or home network. You don't need to expose your server to the internet, reducing attack surface.
- We handle authentication, publishing, security, and deployment for you.
- We don't access, copy, or store your data. We only operate your servers and securely route network traffic from the internet to them.
- We support many application types: Streamlit, FastMCP, FastAPI, vLLM, generic Docker containers, and more.

## Monorepo layout

This repository is an [Nx](https://nx.dev) monorepo (pnpm workspace). All projects live
under `apps/`:

| Project | Stack | Description |
|---------|-------|-------------|
| `ui` | Vite (React) PWA | Web app — consumes the `api` |
| `api` | C# .NET ASP.NET Core | Backend API |
| `tunnel-server` | C# .NET ASP.NET | SSH service that terminates tunnels |
| `churrun-kubernetes` | C# .NET ASP.NET Core | Kubernetes deployment API |
| `churrun-tunnel` | Linux / bash | SSH client script run on private servers |
| `images` | Docker | Collection of tailored container images |

### Common commands

```sh
pnpm install                 # install workspace dependencies
pnpm nx show projects        # list all projects
pnpm nx graph                # open the project graph
pnpm nx <target> <project>   # e.g. pnpm nx serve api, pnpm nx build ui
pnpm nx run-many -t build    # run a target across every project
```

## License

ChurroStack is released under the **GNU Affero General Public License v3.0 (AGPLv3)**.

We chose this license because it ensures ChurroStack remains free and open-source software while protecting against abuse. The AGPLv3 guarantees that:

- **Freedom to use and modify:** Anyone can use, study, modify, and distribute ChurroStack freely.
- **Protection against proprietary forks:** Any modifications or derivative works must also be released under AGPLv3, preventing companies from taking the code, making improvements, and keeping them proprietary.
- **Network copyleft protection:** Unlike standard GPL, the AGPL includes a network clause—if someone runs a modified version of ChurroStack as a service (even without distributing the software), they must make their source code available to users. This prevents the "SaaS loophole" where companies could use our code to build proprietary cloud services.

This license aligns with our mission: empowering users with private, self-hosted infrastructure while ensuring the software remains free and community-driven for everyone.