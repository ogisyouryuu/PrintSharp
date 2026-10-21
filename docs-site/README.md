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

1. 在本目录安装依赖并构建：

   ```powershell
   npm ci
   npm run docs:build
   ```

2. 将 `.vitepress/dist/` **目录中的内容**上传到 VPS 上静态 Web 服务器配置的站点根目录。以下示例假设服务器使用 OpenSSH，并以 `docs.example.com` 和 `/var/www/printsharp-docs/` 作为占位值；请替换为自己的主机名、账号和目录：

   ```powershell
   scp -r .vitepress/dist/* your-user@your-vps:/var/www/printsharp-docs/
   ```

   请确保服务器能读取这些文件，并且站点根目录直接包含 `index.html` 和 `assets/` 等构建内容。

3. 将域名和站点根目录配置到 VPS 已安装的静态 Web 服务器。Nginx 示例：

   ```nginx
   server {
       listen 80;
       server_name docs.example.com;
       root /var/www/printsharp-docs;
       index index.html;

       location / {
           try_files $uri $uri.html $uri/ =404;
       }
   }
   ```

   `try_files` 规则用于匹配门户启用的无 `.html` 页面地址。公网访问时还需按自己的 VPS 环境配置 HTTPS、域名解析和防火墙；这些值不由仓库预设。

默认 `base` 为 `/`，适用于文档站部署在域名根路径的情况。若部署在 `https://example.com/printsharp/` 这样的子路径，在本目录执行：

```powershell
npm run docs:build -- --base /printsharp/
```

然后将生成内容放到服务器对应的 `/printsharp/` 目录。以后文档仍部署在子路径时，每次构建都要带上相同的 `--base`。

## 相关说明

- 网站部署内容是 `.vitepress/dist/`；修改 Markdown 后需重新构建并上传新产物。
- 部署文档门户不会部署或启动 PrintSharp HTTP API。API 的 Linux 发布说明见门户中的[部署与运维文档](docs/operations/deployment.md)。
- 真实 VPS 的发行版、域名、Web 服务器、站点目录和访问方式应以主机实际配置为准；不要将密码、私钥或其他凭据写入仓库。
