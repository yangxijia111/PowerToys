<p align="center">
  <img src="src/settings-ui/Settings.UI/Assets/Settings/Logo.scale-200.png" alt="PowerToys Cuin logo" width="96" />
</p>
<h1 align="center">PowerToys Cuin <sub>(Community Edition)</sub></h1>
<p align="center">
  基于 <a href="https://github.com/microsoft/PowerToys">Microsoft PowerToys</a> 的社区二次开发版本,聚焦中文体验与新手上手引导。<br/>
  A community fork of <a href="https://github.com/microsoft/PowerToys">Microsoft PowerToys</a> focused on the Chinese experience and first-run onboarding.
</p>
<p align="center">
  <a href="#中文">中文</a> | <a href="#english">English</a>
</p>
<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT" /></a>
  <img src="https://img.shields.io/badge/Windows-10%202004%2B%20%2F%2011-blue" alt="Windows 10 2004+ / 11" />
</p>

> [!IMPORTANT]
> 本项目**不是 Microsoft 官方产品**,与 Microsoft 没有从属关系。
> This project is **not an official Microsoft product** and is not affiliated with Microsoft.

<!-- 中文 -->

## 中文

### 简介

**PowerToys Cuin** 保留 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 成熟的核心功能,在此基础上做轻量的二次开发:

- **中文体验优化**:为全部 31 个工具提供面向新手的中文标题与功能说明,并逐步覆盖设置界面高频文案。
- **降低上手门槛**:内置场景化推荐配置(Presets)与首次启动的快速设置向导。
- **信息层级优化**:重新组织设置页导航分组,让工具更容易被找到。

项目处于**早期开发阶段**(详见 [Roadmap](#roadmap路线图)),当前仅建议从源码构建体验,暂不提供安装包下载。

### 与上游的关系

- 本项目 Fork 自 [microsoft/PowerToys](https://github.com/microsoft/PowerToys),核心功能由 Microsoft 及上游社区开发。
- 上游项目采用 [MIT License](LICENSE)(Copyright (c) Microsoft Corporation),本项目沿用该许可证;第三方组件声明见 [NOTICE.md](NOTICE.md)。
- 本项目不冒充官方版本:安装器产品名为 **PowerToys Cuin (Community Edition)**,设置界面与托盘“关于”中均明确标注基于 Microsoft PowerToys (MIT)。
- 需要官方版本请前往 [microsoft/PowerToys](https://github.com/microsoft/PowerToys)。

### 当前改动(Fork 内容)

| 改动 | 说明 |
| --- | --- |
| zh-CN 本地化层 | 新增 `zh-CN` 资源层(上游仓库仅提交 en-us 资源):31 个工具的中文标题与功能说明、设置导航分组、常规设置页高频文案;未覆盖的 key 运行时自动回退英文。附带质量检查脚本(孤儿 key 检测、占位符一致性校验),当前 0 孤儿 key。 |
| 设置导航重组 | 将 31 个工具从 5 个分组重新组织为 6 个分组:系统工具 / 窗口管理 / 输入与启动 / 屏幕与显示 / 文件管理 / 开发与高级。工具的名称、图标、快捷方式均不变。 |
| 场景预设(Presets) | 内置 5 个场景:通用 / 开发 / 学习 / 办公 / 设计。一键批量启用该场景的推荐工具;只启用、不关闭已有功能;可重复应用。 |
| OOBE 快速设置向导 | 首次启动时新增“快速设置”页:选择场景 → 确认推荐工具 → 一键应用,可随时跳过。 |
| 品牌化 | 设置窗口标题、托盘“关于”、安装器产品名品牌化为 PowerToys Cuin (Community Edition);应用图标在上游底图上叠加青色 C 角标;常规设置页新增版本署名卡片(链接上游项目)。内部命名空间、GUID、COM ID 均未改动,保证与上游代码同步的能力。 |

品牌与自定义代码通过 `[fork-brand]` 注释锚点标记,便于与上游同步(见 [同步上游](#同步上游))。

### 构建源码

> 开发环境详细说明见 [doc/devdocs/readme.md](doc/devdocs/readme.md)。

**运行环境**(与上游一致):Windows 10 版本 2004(build 19041)或更高 / Windows 11。

**开发环境**:

- Windows 10 1803 或更新
- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/)(推荐)或 Visual Studio 2022 17.4+,工作负载:Desktop Development with C++、WinUI application development、.NET desktop development,以及 Windows 11 SDK(10.0.22621.0、10.0.26100.3916)
- .NET 8 SDK(构建工具链使用)
- 启用 Windows 长路径支持

**快速开始**:

```powershell
# 1) 克隆仓库
git clone https://github.com/yangxijia111/PowerToys.git
cd PowerToys

# 2) 自动配置开发环境(长路径、开发者模式、VS 组件)
.\tools\build\setup-dev-environment.ps1

# 3) 用 Visual Studio 打开 PowerToys.slnx 构建,
#    或使用构建脚本:
.\tools\build\build.ps1 -Platform x64 -Configuration Release
```

> [!NOTE]
> 本仓库在个人开发环境中构建通过(Release x64 全解决方案、WiX 安装器)。不同机器的工具链版本差异可能带来构建问题,排障记录见 [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md)。

安装器构建使用 `tools\build\build-installer.ps1`(WiX v5,MSI + Bootstrapper)。

### 测试

- `Settings.UI.UnitTests`:MSTest + Moq 的单元测试工程,覆盖设置序列化、Presets 服务逻辑与集成行为(场景目录唯一性、应用幂等性、IPC 状态一致等)。构建后测试输出位于 `Debug\x64\tests\SettingsTests\`,该工程为 Microsoft.Testing.Platform 的可执行程序,可直接运行。
- zh-CN 资源层质量检查:

```powershell
python tools/check_zh_cn_coverage.py --fail-on-orphan
```

输出覆盖统计与缺失 key 清单,孤儿 key(存在于 zh-CN 但不存在于 en-us)会导致非零退出码。

### 本地化(zh-CN)

上游 PowerToys 仓库仅提交 `en-us` 资源,其他语言由微软本地化流程在构建时注入。本 Fork 在仓库内维护一个 `zh-CN` 资源层(`src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw`),采用“高频优先”策略逐步覆盖,未覆盖的 key 自动回退英文。覆盖状态以检查脚本输出为准(截至 2026-09:187 / 2039 个 key,0 孤儿)。

### 项目结构(Fork 自定义区域)

上游代码结构见 [doc/devdocs](doc/devdocs/readme.md)。本 Fork 的新增/修改区域:

| 文件 / 目录 | 内容 |
| --- | --- |
| `src/common/ManagedCommon/Branding.cs` | 集中式品牌常量(`[fork-brand]` 同步锚点) |
| `src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw` | zh-CN 资源层 |
| `src/settings-ui/Settings.UI/Services/Presets/` | 场景预设服务(纯逻辑,可单测) |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/PresetsPage.xaml(.cs)` | 预设推荐页 |
| `src/settings-ui/Settings.UI/SettingsXAML/OOBE/Views/OobePresetPicker.xaml(.cs)` | OOBE 快速设置页 |
| `tools/check_zh_cn_coverage.py` | zh-CN 覆盖检查脚本 |
| `docs/DEVELOPMENT_PLAN.md`、`docs/SYNC_GUIDE.md` | 二开计划、上游同步指南 |

### 同步上游

采用 merge 方式同步 `upstream/main`,保留双方历史;自定义区域以“新增文件为主、修改行数最少”为原则,冲突处理优先级与逐文件应对见 [docs/SYNC_GUIDE.md](docs/SYNC_GUIDE.md)。

```bash
git fetch upstream
git checkout cuin-dev
git merge upstream/main
```

### Roadmap(路线图)

- [x] zh-CN 资源层与工具中文简介
- [x] 设置导航分组重组
- [x] 场景预设(Presets)与 OOBE 快速设置向导
- [x] 品牌化(名称 / 图标 / 安装器产品名)
- [ ] 更多设置页 zh-CN 覆盖(高频优先,持续进行)
- [ ] 首个自有扩展模块(CustomModule)
- [ ] 安装器整合自有语言包;与官方版本共存/迁移说明
- [ ] 公开 Preview 构建

### Contributing

本项目处于早期阶段,欢迎通过 Issue 反馈问题与建议。由于 Fork 的自定义区域以新增文件为主,提交 PR 前建议先开 Issue 讨论。贡献内容默认按本仓库的 MIT 许可证提供。涉及上游功能本身的贡献,请前往 [microsoft/PowerToys](https://github.com/microsoft/PowerToys)。

### License

本项目沿用上游的 [MIT License](LICENSE)。原版权声明(Copyright (c) Microsoft Corporation)与 [NOTICE.md](NOTICE.md) 第三方声明保持不变;本 Fork 的修改同样以 MIT 许可证提供。

### Acknowledgements

- [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 及其所有贡献者 —— 本项目全部核心功能的上游。
- PowerToys 的开源社区(缺陷反馈、设计讨论与文档)。

<!-- English -->

## English

### About

**PowerToys Cuin** keeps the mature core of [Microsoft PowerToys](https://github.com/microsoft/PowerToys) and adds lightweight community enhancements on top:

- **Chinese experience**: beginner-oriented Chinese titles and descriptions for all 31 utilities, plus progressive coverage of high-frequency Settings strings.
- **Lower the barrier to entry**: built-in scenario presets and a first-run quick-setup wizard.
- **Better information hierarchy**: reorganized Settings navigation so utilities are easier to find.

The project is in an **early stage** (see the [Roadmap](#roadmap-1)). For now it is source-build only; no installer downloads are provided yet.

### Relationship with the upstream

- This project is a fork of [microsoft/PowerToys](https://github.com/microsoft/PowerToys); the core features are developed by Microsoft and the upstream community.
- The upstream project is licensed under the [MIT License](LICENSE) (Copyright (c) Microsoft Corporation). This project keeps that license; third-party notices are in [NOTICE.md](NOTICE.md).
- This project does not impersonate the official product: the installer product name is **PowerToys Cuin (Community Edition)**, and the Settings UI and tray About dialog clearly state that it is based on Microsoft PowerToys (MIT).
- For the official product, go to [microsoft/PowerToys](https://github.com/microsoft/PowerToys).

### What this fork changes

| Change | Description |
| --- | --- |
| zh-CN localization layer | Adds a `zh-CN` resource layer (the upstream repo commits en-us resources only): Chinese titles and descriptions for all 31 utilities, Settings navigation groups, and high-frequency strings on the general settings page. Uncovered keys fall back to English at runtime. A quality-check script (orphan-key detection, placeholder consistency) is included; currently 0 orphan keys. |
| Settings navigation regrouping | Reorganizes the 31 utilities from 5 groups into 6: system tools / window management / input & launcher / screen & display / file management / developer & advanced. Names, icons, and shortcuts stay unchanged. |
| Scenario presets | 5 built-in presets: General / Developer / Learning / Office / Design. One click enables the recommended utilities for a scenario; it only enables, never disables, existing features and can be re-applied. |
| OOBE quick setup | A new "Quick setup" page on first run: pick a scenario → confirm the recommended utilities → apply in one click. Skippable at any time. |
| Branding | Settings window titles, tray About, and the installer product name are branded as PowerToys Cuin (Community Edition); the app icon adds a cyan "C" badge on top of the upstream icon; the general settings page gains an attribution card linking to the upstream project. Namespaces, GUIDs, and COM IDs are untouched to keep upstream syncs manageable. |

Branding and custom code are marked with `[fork-brand]` comment anchors for upstream syncing (see [Syncing with upstream](#syncing-with-upstream)).

### Building from source

> Detailed developer documentation: [doc/devdocs/readme.md](doc/devdocs/readme.md).

**Runtime requirements** (same as upstream): Windows 10 version 2004 (build 19041) or newer / Windows 11.

**Development environment**:

- Windows 10 1803 or newer
- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) (recommended) or Visual Studio 2022 17.4+ with: Desktop Development with C++, WinUI application development, .NET desktop development, plus Windows 11 SDKs (10.0.22621.0, 10.0.26100.3916)
- .NET 8 SDK (used by the build tooling)
- Windows long path support enabled

**Quick start**:

```powershell
# 1) Clone the repository
git clone https://github.com/yangxijia111/PowerToys.git
cd PowerToys

# 2) Automated environment setup (long paths, developer mode, VS components)
.\tools\build\setup-dev-environment.ps1

# 3) Open PowerToys.slnx in Visual Studio, or use the build script:
.\tools\build\build.ps1 -Platform x64 -Configuration Release
```

> [!NOTE]
> This repository has been built successfully in the maintainer's personal environment (Release x64 full solution and the WiX installer). Toolchain differences may cause issues on other machines; troubleshooting notes are in [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md).

The installer is built with `tools\build\build-installer.ps1` (WiX v5, MSI + Bootstrapper).

### Tests

- `Settings.UI.UnitTests`: an MSTest + Moq unit test project covering settings serialization and the Presets service (catalog uniqueness, idempotent apply, IPC state consistency, etc.). After building, the test output lands in `Debug\x64\tests\SettingsTests\`; the project is an executable based on Microsoft.Testing.Platform and can be run directly.
- zh-CN resource layer quality check:

```powershell
python tools/check_zh_cn_coverage.py --fail-on-orphan
```

It prints coverage stats and the list of missing keys; orphan keys (present in zh-CN but not in en-us) cause a non-zero exit code.

### Localization (zh-CN)

The upstream PowerToys repository commits `en-us` resources only; other languages are injected by Microsoft's localization pipeline at build time. This fork maintains a `zh-CN` resource layer inside the repository (`src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw`), following a "high-frequency first" strategy with automatic English fallback for uncovered keys. Coverage status is whatever the check script reports (as of 2026-09: 187 / 2039 keys, 0 orphans).

### Project structure (fork-specific areas)

For the upstream architecture see [doc/devdocs](doc/devdocs/readme.md). Areas added or modified by this fork:

| File / directory | Purpose |
| --- | --- |
| `src/common/ManagedCommon/Branding.cs` | Central branding constants (`[fork-brand]` sync anchors) |
| `src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw` | zh-CN resource layer |
| `src/settings-ui/Settings.UI/Services/Presets/` | Scenario preset service (pure logic, unit-testable) |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/PresetsPage.xaml(.cs)` | Presets recommendation page |
| `src/settings-ui/Settings.UI/SettingsXAML/OOBE/Views/OobePresetPicker.xaml(.cs)` | OOBE quick setup page |
| `tools/check_zh_cn_coverage.py` | zh-CN coverage check script |
| `docs/DEVELOPMENT_PLAN.md`, `docs/SYNC_GUIDE.md` | Development plan, upstream sync guide |

### Syncing with upstream

The fork syncs `upstream/main` via merge, preserving both histories. Custom areas are kept as "new files where possible, minimal edits otherwise". Conflict priorities and per-file guidance live in [docs/SYNC_GUIDE.md](docs/SYNC_GUIDE.md).

```bash
git fetch upstream
git checkout cuin-dev
git merge upstream/main
```

### Roadmap

- [x] zh-CN resource layer and Chinese descriptions for all utilities
- [x] Settings navigation regrouping
- [x] Scenario presets and OOBE quick setup
- [x] Branding (name / icon / installer product name)
- [ ] More zh-CN coverage in Settings (high-frequency first, ongoing)
- [ ] First custom module (CustomModule)
- [ ] Installer with the fork language pack; coexistence/migration notes with the official build
- [ ] Public Preview builds

### Contributing

The project is in an early stage — issues for feedback and bug reports are welcome. Since fork changes favor new files over edits, please open an issue before submitting large PRs. Contributions are provided under this repository's MIT license. Contributions to the upstream product itself belong in [microsoft/PowerToys](https://github.com/microsoft/PowerToys).

### License

This project keeps the upstream [MIT License](LICENSE). The original copyright notice (Copyright (c) Microsoft Corporation) and the third-party notices in [NOTICE.md](NOTICE.md) remain unchanged; the fork's modifications are likewise provided under the MIT license.

### Acknowledgements

- [Microsoft PowerToys](https://github.com/microsoft/PowerToys) and all of its contributors — the upstream of every core feature here.
- The PowerToys open source community (bug reports, design discussions, and documentation).
