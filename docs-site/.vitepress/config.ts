import { defineConfig } from 'vitepress'

const gettingStarted = [
  { text: '阅读指南', link: '/getting-started/' },
  { text: '开发环境', link: '/getting-started/development-environment' },
  { text: '构建与测试', link: '/getting-started/build-and-test' },
  { text: '项目结构', link: '/getting-started/project-structure' },
]

const architecture = [
  { text: '系统概览', link: '/architecture/system-overview' },
  { text: '架构决策', link: '/architecture/design-decisions' },
]

const api = [{ text: 'API 参考', link: '/api-reference/' }]
const examples = [{ text: '使用示例', link: '/examples/' }]
const operations = [
  { text: '部署与运维', link: '/operations/' },
  { text: '配置', link: '/operations/configuration' },
  { text: '部署', link: '/operations/deployment' },
  { text: '故障排查', link: '/operations/troubleshooting' },
]

export default defineConfig({
  lang: 'zh-CN',
  title: 'PrintSharp 文档中心',
  description: 'PrintSharp 开发、架构、API 与部署文档。',
  srcDir: './docs',
  outDir: './.vitepress/dist',
  cleanUrls: true,
  lastUpdated: true,
  themeConfig: {
    siteTitle: 'PrintSharp',
    nav: [
      { text: '开始使用', link: '/getting-started/' },
      { text: '架构设计', link: '/architecture/system-overview' },
      { text: 'API 参考', link: '/api-reference/' },
      { text: '使用示例', link: '/examples/' },
      { text: '部署运维', link: '/operations/' },
    ],
    sidebar: {
      '/getting-started/': [{ text: '开始使用', items: gettingStarted }],
      '/architecture/': [{ text: '架构设计', items: architecture }],
      '/api-reference/': [{ text: 'API 参考', items: api }],
      '/examples/': [{ text: '使用示例', items: examples }],
      '/operations/': [{ text: '部署与运维', items: operations }],
    },
    search: {
      provider: 'local',
      options: {
        locales: {
          root: {
            translations: {
              button: { buttonText: '搜索文档', buttonAriaLabel: '搜索文档' },
              modal: {
                noResultsText: '未找到相关结果',
                resetButtonTitle: '清除查询条件',
                footer: { selectText: '选择', navigateText: '切换', closeText: '关闭' },
              },
            },
          },
        },
      },
    },
    editLink: {
      pattern: 'https://github.com/ogisyouryuu/PrintSharp/edit/main/docs-site/docs/:path',
      text: '在 GitHub 上编辑此页',
    },
    docFooter: { prev: '上一页', next: '下一页' },
    outline: { label: '本页目录', level: [2, 3] },
    lastUpdated: { text: '最后更新于' },
    footer: {
      message: '文档内容以仓库当前实现为准。',
    },
  },
})
