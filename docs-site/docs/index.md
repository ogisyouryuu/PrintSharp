# PrintSharp 文档中心

PrintSharp 文档中心汇集项目的开发、架构、API、示例和部署资料。它是随仓库维护的 Markdown 文档，并由 VitePress 构建为可独立托管的静态网站。

## 从这里开始

- [阅读指南](/getting-started/)：了解文档分区和建议阅读顺序。
- [开发环境](/getting-started/development-environment)：准备 .NET SDK 和常用工具。
- [构建与测试](/getting-started/build-and-test)：查看解决方案构建、测试和演示入口。
- [项目结构](/getting-started/project-structure)：了解当前解决方案中的项目职责。

## 理解系统

- [系统概览](/architecture/system-overview)：从仓库代码出发了解核心库、渲染器和 API 之间的关系。
- [架构决策](/architecture/design-decisions)：查看决策记录约定和当前已收录的决策。

## 按任务查找

- 调用 HTTP 服务：先看 [API 参考](/api-reference/)，再读 [API 项目使用说明](https://github.com/ogisyouryuu/PrintSharp/blob/main/src/PrintSharp.Api/README.md)。
- 查找代码和模板示例：打开[使用示例](/examples/)及仓库中的 WinForms Demo。
- 发布服务或排查 Linux PDF 输出：从[部署与运维](/operations/)进入。

## 文档状态约定

- **当前实现**：可以从现有代码、项目配置或已记录的验证结果中确认。
- **设计规范**：用于约束未来变更；除非明确注明已实现，否则不代表现有行为。
- **规划中 / 待确认**：仓库尚无实现或足够证据，后续需要维护者核实。

README 的英文、简体中文和日文版本继续作为仓库主页入口。门户源文件位于 `docs-site/docs/`；原始发布记录、迁移记录和验证材料仍保留在仓库原路径，避免破坏现有链接和工作流程。

## 维护原则

代码、API、配置和部署方式发生变化时，应在同一变更中更新对应文档。示例命令以仓库根目录为起点并注明执行目录；敏感配置只描述变量名和安全做法，不提交真实凭据。发现不准确内容时，以代码和可重复验证记录为依据修正。
