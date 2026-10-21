# 部署与运维

本分区整理 API 配置、Linux 发布和故障定位入口。文档站自身是静态文件，可部署到 VPS 上的静态 Web 服务器；它与 PrintSharp API 服务部署相互独立。

## 阅读入口

- [配置项](/operations/configuration)：查看已提交的默认 API 资源限制。
- [部署](/operations/deployment)：查看仓库已有的 Linux API 发布记录和静态文档构建方式。
- [故障排查](/operations/troubleshooting)：按已验证证据区分解决办法和建议检查项。

生产拓扑、域名、反向代理及 VPS 地址不在仓库中，因此不作为既定配置描述。API 对外访问前的认证、HTTPS 和访问策略由实际部署环境确认。
