# 二次开发计划（DEVELOPMENT_PLAN）

> 本文档是本 Fork（`yangxijia111/PowerToys`）的长期二次开发总纲。
> 基于对上游 `microsoft/PowerToys` main 分支（2026-09，commit `dd65f4017`）的代码审计撰写。
> 开发分支：`cuin-dev`。

## 1. 项目定位

保留 PowerToys 成熟可靠的核心功能，重点做：

- **中文体验优化**（本地化翻译、术语修正、每工具中文简介）
- **UI 美化 / 信息层级优化**（Settings 导航、分类、视觉）
- **降低上手门槛**（首次启动场景化引导、推荐配置）
- **少量独立扩展**（新增自有工具模块，不动核心）

**不做**：无意义重写底层、批量重构 namespace/GUID、伪装官方产品。

## 2. 项目架构（上游现状）

```
PowerToys.slnx                     # 总解决方案（新 XML 格式）
├─ src/runner/                     # C++ 主控进程：加载全部模块 DLL、IPC 中枢、托盘、热键
│   └─ main.cpp knownModules       # ★ 硬编码的模块 DLL 清单（新模块注册点）
├─ src/modules/                    # 30+ 工具模块（每个实现 PowertoyModuleIface）
│   └─ interface/                  # 模块 C++ 接口定义（powertoy_create 导出约定）
├─ src/settings-ui/                # ★ Settings 应用（WinUI 3，独立进程，与 runner 管道通信）
│   ├─ SettingsXAML/ShellPage.xaml # ★ 左侧导航（硬编码 5 个分组 + 各模块项）
│   ├─ Strings/en-us/Resources.resw# ★ 全部 UI 字符串源（仅 en-us 进 git！）
│   ├─ ViewModels/  Views/         # 每模块一对 Page + ViewModel
│   └─ OOBE/ OobeWindow            # 首启向导（oobe_settings.json 控制）
├─ src/common/                     # C++/C# 公共库（SettingsAPI、logger、GPO、interop…）
├─ src/gpo/assets/                 # 组策略模板（admx/adml）
├─ installer/PowerToysSetupVNext/  # WiX v5 安装器（MSI + Burn 引导器）
└─ tools/build/                    # build.ps1 / build-essentials.ps1 / build-installer.ps1
```

模块加载流程：runner `LoadLibraryW` → `GetProcAddress("powertoy_create")` → `PowertoyModuleIface*`（get/set_config、enable/disable、热键、GPO）。Settings UI（C#/WinUI 3）通过命名管道与 runner 双向同步配置；配置落盘于 `%LOCALAPPDATA%\Microsoft\PowerToys\<模块>\settings.json`。

## 3. 主要技术栈

| 层 | 技术 |
|---|---|
| 主控进程 runner | C++20（v143 工具集、ATL/WTL、/W4 + WX、静态 MT） |
| Settings UI | WinUI 3（Windows App SDK，非打包部署），C# `net10.0-windows10.0.26100` |
| 模块 | C++（多数）+ C#（部分，如 Awake、Peek、CmdPal） |
| 依赖 | NuGet（托管侧）+ vcpkg manifest（原生侧仅 libwebp、spdlog） |
| 安装器 | WiX v5（MSI + Burn bootstrapper） |
| 测试 | MSTest + Microsoft.Testing.Platform（C#）、CppUnitTest（C++）、WinAppDriver/UIA（UI 测试） |
| 本地化 | 仓库仅含 en-us resw，其余语言由微软 loc farm 构建时注入 |

## 4. UI / Localization / Module / Installer 的关系

- **Module → UI**：模块只提供 `PowertoyModuleIface`；Settings 导航项在 `ShellPage.xaml` 硬编码，页面路由在 `App.GetPage()`，元数据（标题/图标/启用态）统一走 `Settings.UI.Library/Helpers/ModuleHelper.cs`（资源 key 模式 `{Module}.ModuleTitle` / `{Module}.ModuleDescription`）。
- **UI → Localization**：XAML 通过 `x:Uid` 绑定 resw key；en-us 是唯一源语言，`{Module}.ModuleDescription` 即"工具一句话简介"。语言列表硬编码在 `installer/PowerToysSetupVNext/Resources.wxs` 与 `GeneralViewModel.cs`（两处需同步）。
- **Module → Installer**：每个模块一个 `.wxs` 组件组，`Product.wxs` 引用；runner `knownModules` 与安装器文件清单需保持一致。
- **一切配置**：经 IPC 汇聚到 runner 落盘；GPO（`HKLM/HKCU\SOFTWARE\Policies\PowerToys`）可锁定开关。

## 5. 本地化策略（关键决策）

上游 zh-CN 资源**不进 git**（loc farm 注入），因此本 Fork 的中文优化采用 **"新增 `Strings/zh-CN/` 目录"** 方式：

- 新增 `src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw`（及 Controls 库同名文件）——纯增量文件，不碰 upstream 任何文件，构建时自动并入 PRI。
- 用户系统语言为 zh-CN 或在设置中切换语言后自动生效（`ApplicationLanguages.PrimaryLanguageOverride` 机制）。
- **不直接修改 en-us resw**（上游高频改动文件，且是 loc farm 的源）；确需改英文文案的点单独评估。
- 与 Crowdin 无冲突：我们不参与上游翻译流程，本 Fork 的 zh-CN 是独立维护层。

## 6. 修改区域规划（低耦合原则）

| 区域 | 目标 | 主要文件（新增为主） | 冲突风险 |
|---|---|---|---|
| Branding | 自有品牌显示名/图标，与官方区分 | 少量资源与显示字符串；**不改 namespace/GUID/COM ID** | 低 |
| Localization zh-CN | 术语修正 + 每工具中文简介 | `Strings/zh-CN/*.resw`（全新增） | 极低 |
| Categories | 工具重新分类（文件/窗口/输入/屏幕/开发/系统/高级） | `ShellPage.xaml` 分组结构 + 分组标题资源 | 中（上游高频文件，改动保持最小） |
| Onboarding | 首启场景推荐（开发/学习/办公/设计/通用） | 复用 `OobeWindow`，新增 OOBE 推荐页 | 低 |
| Presets | 一键推荐配置架构 | 新增 `Presets` 服务 + 数据 JSON（`Settings.UI.Library` 内新增） | 低 |
| CustomModules | 未来独立工具 | `src/modules/custom/`（新目录）+ runner `knownModules` 追加 | 低 |

**尽量保持与 upstream 一致的区域（原则：只读）**：

- `src/runner/` 全部 C++ 核心
- `src/modules/` 全部既有模块的功能实现
- `src/common/` 公共库
- `src/settings-ui` 的 ViewModel 业务逻辑、设置持久化（`SettingsUtils`、`*.Settings.cs`）
- 构建脚本、CI、vcpkg 清单、安装器核心逻辑
- `LICENSE`（MIT + Microsoft 版权必须保留）、`NOTICE.md`、第三方许可

## 7. upstream 同步策略

```bash
git fetch upstream
git checkout cuin-dev
git merge upstream/main        # 或 rebase（历史干净但需逐冲突处理，推荐 merge）
```

预计冲突热点（按频率排序）：

1. `src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml` —— 上游每加一个模块都改导航；我们的分类改动集中在同一文件。缓解：分组改动保持"移动现有项、不改 ID"，合并时逐项核对。
2. `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` —— 上游高频追加 key；我们原则上不改此文件。
3. `App.xaml.cs`（GetPage 路由）—— 上游加模块时改；我们仅在新增自有模块时追加。
4. `Settings.UI.Library/Helpers/ModuleHelper.cs`、OOBE 相关 —— 中频。
5. 品牌资源与 `Version.props` —— 低频，独立维护。

同步后必须：全量 Build + 单元测试回归，再继续开发。

## 8. 构建与验证命令

```powershell
# 环境：VS 2022 17.14+（.vsconfig 组件：ATL、SDK 22621/26100、WindowsAppSDK、vcpkg）、.NET 10 SDK、Windows 长路径已启用
.\tools\build\build-essentials.ps1          # 快速验证：runner + Settings
.\tools\build\build.ps1 -Platform x64 -Configuration Release   # 全量
# 输出：仓库根 x64\<Config>\（WinUI3 应用在 WinUI3Apps\ 子目录）
# 测试：dotnet test（C#，MSTest Runner）；C++ 测试经 msbuild /t:Test（vstest）
```

基线原则：任何基线问题先记录（本文档第 9 节），不用大范围修改绕过。

## 9. 基线问题记录

（基线构建/测试过程中发现的问题在此追加，附日期与处理方式）

- 2026-09-26：开发机为 VS 2022 BuildTools 17.14，缺少 ATL / SDK 22621 / WindowsAppSDK 支持组件，已按 `.vsconfig` 补装（非代码改动）。

## 10. 阶段路线图

- **阶段一（当前）**：框架层——zh-CN 资源层 + 工具中文简介、分类梳理、Onboarding 场景推荐骨架、Presets 架构预留。功能行为零变更。
- **阶段二**：品牌化（名称/图标）+ Settings 视觉打磨 + 首个 CustomModule。
- **阶段三**：安装器整合自有语言包与品牌，发布流程（含与官方版本的共存/迁移说明）。
