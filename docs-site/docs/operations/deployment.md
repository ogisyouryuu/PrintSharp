# 部署

PrintSharp 的文档门户与 HTTP API 已部署在 `23.238.1.37`（AlmaLinux 9.6）：

| 服务 | 地址 | 服务器运行方式 |
|---|---|---|
| 文档门户 | <https://printsharp-docs.ginbow.eu.org> | Nginx 托管 VitePress 静态文件 |
| PrintSharp API | <https://printsharp.ginbow.eu.org> | Nginx 反向代理到本机 `127.0.0.1:5080` |

两个域名使用同一张 Let’s Encrypt 证书，Certbot 自动续期。API 由 `printsharp-api.service` 管理，服务器不直接开放 API 进程端口。Nginx 对 API 请求限制为每个 IP 每秒 10 个请求、最多突发 10 个请求，最大请求体为 21 MiB；API 自身的请求上限仍为 20 MiB。

## 一键发布

在仓库根目录使用 PowerShell 执行：

```powershell
.\eng\deploy.ps1
```

脚本会构建 Linux x64 自包含 API、安装并构建文档站、通过 SSH 上传到独立版本目录，再切换 `current` 符号链接。API 发布后会重启 systemd 服务并检查健康接口；文档发布后会通过 HTTPS 检查首页。发布单项时使用：

```powershell
.\eng\deploy.ps1 -Target api
.\eng\deploy.ps1 -Target docs
```

默认 SSH 主机别名为本机 `~/.ssh/config` 中的 `pms-vps`（root@23.238.1.37）。如果你的 SSH 配置使用其他 root 主机别名，可传入：

```powershell
.\eng\deploy.ps1 -HostName root@host
```

SSH 密钥需已在本机配置。发布产物和临时压缩包放在 `artifacts/deploy/`，不会提交到 Git。

## HTTPS 与访问范围

HTTP 自动跳转到 HTTPS。证书覆盖上述两个主机名，Certbot 已设置自动续期任务。可以用以下地址确认 API 服务：

```text
https://printsharp.ginbow.eu.org/api/v1/health
```

API 目前没有内置身份认证，生成接口可被公网访问。若业务数据不应由任何互联网用户提交，应在调用方启用前增加 API Key 或其他身份认证机制，并在 Nginx 配置相应访问策略。文档站为公开站点。
