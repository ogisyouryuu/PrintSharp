# PrintSharp 文档站

这里维护 PrintSharp 文档门户的 VitePress 工程。Markdown 页面位于 `docs/`，VitePress 配置位于 `.vitepress/`。文档站独立于 C# 解决方案；部署后的站点是静态文件，不需要在 VPS 上运行 Node.js、.NET、数据库或 PrintSharp API。

## 环境要求

- Node.js 22 或更高版本
- npm

请在本目录（仓库根目录的 `docs-site/`）执行下列命令。

## 安装依赖

首次安装或更新依赖后，使用 lockfile 还原：

```powershell
npm ci
```

升级 VitePress 等依赖时使用 `npm install`，确认变更后提交 `package.json` 和 `package-lock.json`。

## 本地开发

```powershell
npm run docs:dev
```

VitePress 会启动开发服务器并显示本地访问地址。修改 `docs/` 下的 Markdown 页面或 `.vitepress/config.ts` 后，浏览器会自动刷新。

## 正式构建

```powershell
npm run docs:build
```

静态站点生成到 `.vitepress/dist/`。可在构建机本地直接托管这个目录进行预览：

```powershell
npm run docs:preview
```

预览服务器只用于检查构建结果，不是 VPS 的生产服务。提交源码时不需要提交 `node_modules/`、`.vitepress/cache/` 或 `.vitepress/dist/`；这些目录已加入仓库忽略规则。

## 部署到 VPS

文档门户发布在 <https://printsharp-docs.ginbow.eu.org>。在仓库根目录用 PowerShell 执行一键发布脚本：

```powershell
.\eng\deploy.ps1 -Target docs
```

脚本执行 `npm ci` 与正式构建，然后通过 SSH 将 `.vitepress/dist/` 上传到 VPS 的版本目录，最后切换 `/var/www/printsharp-docs/current` 并检查 HTTPS 首页。SSH 默认使用本机 `pms-vps` 主机别名；其他有 root 访问权限的主机可使用 `-HostName root@host`。详细信息见门户的[部署与运维文档](docs/operations/deployment.md)。

默认 `base` 为 `/`，适用于文档站部署在域名根路径的情况。若部署在 `https://example.com/printsharp/` 这样的子路径，在本目录执行：

```powershell
npm run docs:build -- --base /printsharp/
```

然后将生成内容放到服务器对应的 `/printsharp/` 目录。以后文档仍部署在子路径时，每次构建都要带上相同的 `--base`。

## 相关说明

- 网站部署内容是 `.vitepress/dist/`；修改 Markdown 后需重新构建并上传新产物。
- 部署文档门户不会部署或启动 PrintSharp HTTP API。API 的 Linux 发布说明见门户中的[部署与运维文档](docs/operations/deployment.md)。
- 真实 VPS 的发行版、域名、Web 服务器、站点目录和访问方式应以主机实际配置为准；不要将密码、私钥或其他凭据写入仓库。
