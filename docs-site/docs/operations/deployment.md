# 部署

本页区分两类部署：VitePress 文档门户生成静态文件；PrintSharp API 是单独的 ASP.NET Core 服务。静态门户本身不需要 .NET、数据库或业务 API。

## 构建文档门户

在仓库根目录执行：

```powershell
Set-Location docs-site
npm ci
npm run docs:build
```

默认静态输出为 `docs-site/.vitepress/dist/`。将该目录内容同步到 VPS 上配置好的静态站点目录即可。默认 `base` 为 `/`，适用于站点挂载在域名根路径的情形；若部署到子路径，应设置对应 VitePress `base` 后重新构建。具体 Nginx/Caddy、TLS、域名和同步方式取决于尚未提供的 VPS 拓扑，当前标记为待确认。

## API Linux 发布

仓库的 API README 记录了 Rocky Linux x64 自包含发布命令：

```powershell
dotnet publish src/PrintSharp.Api -c Release -r linux-x64 --self-contained true -o artifacts/publish/skia-linux-x64
```

已有验证说明在 Rocky 上需要 `fontconfig`、`libicu`、`openssl-libs`，并应完整保留发布目录中的字体、许可文件及 Skia/HarfBuzz 原生库。真实生产主机尚未连接或验证；ARM64 也需独立验证。实际运行用户、反向代理、认证、域名和防火墙由部署环境确认。

## VPS 信息待确认

仓库没有 VPS 操作系统版本、架构、静态服务器软件、域名或目录权限信息。部署到用户私人 VPS 的具体配置步骤需据目标主机核实；本门户交付的是可托管的静态构建产物。
