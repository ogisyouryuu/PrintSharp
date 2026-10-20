# NuGet 发布说明

PrintSharp 通过 GitHub Actions 向 NuGet.org 发布。发布使用 NuGet Trusted Publishing（OIDC），不需要在本机保存或传递 NuGet API Key。

## 发布前配置

以下配置在首次发布前完成一次：

1. 在 GitHub 仓库 `Settings → Secrets and variables → Actions → Variables` 中添加 `NUGET_USER`，值为 NuGet.org 的个人资料名称。
2. 在 NuGet.org 的 Trusted Publishing 中添加 GitHub Actions 发布者：
   - Package owner：`ogisyouryuu`
   - Repository owner：`ogisyouryuu`
   - Repository：`PrintSharp`
   - Workflow：`nuget.yml`
   - 权限：允许发布新的包和版本，包范围为 `PrintSharp*`

不要为常规发布创建或复制长期 API Key。工作流通过 GitHub OIDC 获取短期发布凭据。

## 发布步骤

1. 确认要发布的改动已合并到 `main`，工作区干净，并且新版本没有使用过。NuGet 已发布的版本不能覆盖。
2. 选择新的 `MAJOR.MINOR.PATCH` 版本。工作流只接受 `vX.Y.Z` 格式，例如 `v0.1.2`；不要复用已有标签或 NuGet 版本。
3. 在仓库根目录创建并推送标签：

   ```powershell
   git switch main
   git pull --ff-only
   git status --short
   git tag v0.1.2
   git push origin v0.1.2
   ```

   将示例中的 `0.1.2` 替换为这次要发布的版本。`git status --short` 应没有输出；如果工作区不干净，先处理改动再发布。

4. 打开 GitHub 仓库的 **Actions → NuGet Publish**，等待由该标签触发的运行完成，并确认结论为 `success`。
5. 在 NuGet.org 检查四个包的新版本：`PrintSharp`、`PrintSharp.Excel`、`PrintSharp.Pdf` 和 `PrintSharp.Windows`。推送成功后 NuGet 还会验证并建立索引；通常 15 分钟内可见。等待期间可在账户的 **Manage Packages → Unlisted Packages** 查看状态。若一小时后仍未发布，检查 [NuGet 服务状态](https://status.nuget.org/) 并联系 NuGet 支持。

## 工作流做什么

`.github/workflows/nuget.yml` 从标签读取版本，然后依次恢复依赖、构建解决方案、运行测试、打包四个库、验证包内容并构建消费端示例，最后通过 OIDC 登录并推送 `.nupkg`。发布过程不需要在开发机运行 `dotnet nuget push`。

若工作流在推送前因临时服务问题失败，可以重新运行该工作流；若是代码或配置错误，先修复并使用新的版本标签发布。若推送阶段只成功发布了部分包，不要重用或覆盖该版本；NuGet 版本不可变，应选择新的版本号，并先确认各包当前发布状态。
