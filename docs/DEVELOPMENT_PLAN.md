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

- 2026-09-26：开发机原仅 VS 2022 BuildTools 17.14，且缺少 ATL / SDK 22621 / WindowsAppSDK 组件；已补装。**注意：VS 2022（17.x）的 MSBuild SDK 解析器不支持 .NET 10 SDK**，对 main 分支（`net10.0-windows10.0.26100.0`）全量 Restore 时报 `NETSDK1045`（回落到 SDK 9.0.316）。这与官方文档"推荐 VS 2026"一致，属于环境问题而非代码问题。
- 2026-09-26：解决方案：安装 **VS 2026 BuildTools（18.10，MSVC v145）** 到 `D:\VS2026BuildTools`，含 C++/.NET 桌面/UWP 工作负载、ATL、SDK 22621+26100、WindowsAppSDK 支持、vcpkg。`build-common.ps1` 的 `Ensure-VsDevEnvironment` 会通过 vswhere 自动选中该最新实例；VS 2022 实例保留不动。上游 `Cpp.Build.props` 已适配 VS18（自动切 v145 工具集），无需改代码。
- 2026-09-26：Azure DevOps 私有 NuGet 源（`pkgs.dev.azure.com/shine-oss`）在国内网络下载大包（ARM64 runtime、WindowsAppSDK 等）易超时；解决方式为 restore 前设置 `NUGET_HTTP_CACHE_TIMEOUT=1800000`（30 分钟）重试（Restore 幂等，已下载包有本地缓存）。未修改仓库 `nuget.config`。
- 2026-09-26：**基线构建结果（x64 Debug）**：`tools\build\build-essentials.ps1` Restore + Build 全部通过（0 error）；产物 `x64\Debug\PowerToys.exe`（runner）与 `x64\Debug\WinUI3Apps\PowerToys.Settings.exe` 均生成并可启动运行。**测试结果**：`Settings.UI.UnitTests` 全套 328/328 通过（含新增 Presets 6 项）。新增代码需遵守仓库 StyleCop 强制规则（单文件单类型 SA1402、文件名匹配首类型 SA1649、`ArgumentNullException.ThrowIfNull` CA1510、格式化需 IFormatProvider CA1305）。

### Phase 2 构建验证记录（2026-09-27）

- **环境**：VS 2026 Community 18.10（`D:\Develop\VS_2026`），工作负载 C++ 桌面 + .NET 桌面；**关键组件** `Microsoft.VisualStudio.ComponentGroup.UWP.VC`（.vsconfig 原始 ID 在 Community 上可安装；`*BuildTools` 后缀变体与 `v143` 变体在 Community 上均 Non-installable）。.NET SDK 10.0.302/10.0.401。
- **VS2026 兼容问题与修复**（全部为环境/上游缺陷，未削弱任何测试）：
  1. `AutoHideCursorWorker.vcxproj` 缺 `deps/spdlog.props` 导入（上游缺陷，共享 OutDir 下 PCH 宏不一致 → C4651/C2220）→ 已修复并提交；
  2. PowerRenameUI 等 WinUI C++ 项目首次构建需 Restore + UWP 组件（`/t:Restore /p:RestorePackagesConfig=true`）；
  3. `.NET SDK 10.0.401` 需要重新拉取对应 runtime 包（`NUGET_HTTP_CACHE_TIMEOUT=1800000`）；
  4. CsWinRT 需要 `TargetPlatformMinVersion=10.0.19041` 的 UAP Platform.xml 与 References——Windows Kits 目录下已用 junction `10.0.19041.0 → 10.0.26100.0` 补齐（仅本机环境）；
  5. WiX CustomActions 链接需要 `wixtoolset.wcautil/dutil` 的 lib——packages.config 还原到 `installer\packages`，构建时将 wcautil.lib/dutil.lib 复制到 `vcpkg_installed\...\lib`（仅本机环境）；
  6. WiX PreBuildEvent 的 `publish.cmd` 相对路径为上游本地构建 bug（CI 走 IsPipeline 分支）→ 本地手动执行 `publish.cmd x64` + `generateMonacoWxs.ps1`，构建时传 `/p:PreBuildEvent=` 跳过；
  7. CmdPal msix 需 `CIBuild=true` 构建 CmdPal.UI 生成。
- **结果**：
  - Full Solution Release Build（PowerToys.slnx）✅ 0 error
  - BugReportTool / StylesReportTool ✅
  - WiX Installer（MSI）+ Bootstrapper ✅ → `PowerToysUserSetup-0.0.1-x64.exe/.msi`（320MB）
  - **安装**（msiexec 静默）✅ → `%LOCALAPPDATA%\PowerToys`
  - **Smoke Test**：runner + Settings + 模块进程（FancyZones/Awake/ColorPicker/QuickAccess/AlwaysOnTop）✅；OOBE 首启标志写入 ✅；settings.json 持久化 ✅；PowerRename Shell Extension 注册 ✅；FileLocksmith/ImageResizer CLI ✅
  - **卸载**（msiexec /x 静默）✅（残留 `PowerToys\BuildTools` 空目录，与官方卸载行为一致）

### Phase 3 发行身份审计与隔离（2026-09-27）

**审计**：对 `installer/`、`src/runner/`、`src/common/`、`src/modules/`、`src/settings-ui/`、`src/PackageIdentity/` 全量盘点发行身份（MSI/Bundle UpgradeCode、COM CLSID、sparse MSIX、协议、AUMID、mutex/事件/pipe、计划任务、AppData、更新信任链），结论与四类分级见 `IDENTITY_AUDIT.md`，逐项映射见 `IDENTITY_MAP.md`。

**发行策略：互斥安装**。审计确认 sparse MSIX（4 个右键菜单包 + PackageIdentity）同 Name+Publisher 互抢且 fork 无法安全分叉微软签名信任链；17+ COM CLSID 共存需横跨 20+ 文件改动。故 fork 安装器主动检测官方（两个官方 UpgradeCode → Bundle bal:Condition + MSI OnlyDetect Upgrade 双层阻止），COM/协议/AUMID/计划任务等系统单点保留官方值，由互斥保证唯一持有者；runner mutex 保留同名作为违规并装时的最后安全带（同会话仅一个 runner 可活）。

**已隔离**：MSI UpgradeCode ×2 + Bundle UpgradeCode（fork 专属新 GUID）、Manufacturer（`PowerToys Cuin Community`）、安装目录 `PowerToysCuin`、快捷方式名、AppData 根 `%LOCALAPPDATA%\PowerToysCuin`（C++/托管双端集中常量，不迁移官方数据）、更新端点指向 fork 仓库、更新引导器产品名锚、pipe 鉴权关闭微软签名项（fork 无微软签名，Release 版 Settings IPC 必需）、DSC 卸载项检测名、MsiUtils/CA 运行时身份常量（含修复旧组件 GUID 失配）。

**验证资产**：`tools/check_fork_identity.py`（静态校验，29 项断言）+ `Settings.UI.UnitTests/ForkIdentityTests.cs`（4 项）。上游合并时按 `SYNC_GUIDE.md` §1.1 身份热点表核对，`[fork-identity]` 锚点 hunk 保留 fork 侧。

**已知限制**（详见 AUDIT §4/§6）：fork 无代码签名证书 → 5 个 sparse MSIX 在最终用户机上注册失败（安全降级，Win11 右键菜单不可用，Win10 经典菜单不受影响）；官方安装器感知不到 fork，官方后装可覆盖运行面（文件层安全，目录已隔离）；fork 自更新信任链待引入签名证书后重设计（0.0.x guard 下当前不激活）。

## 10. 阶段路线图

- **阶段一（当前）**：框架层——zh-CN 资源层 + 工具中文简介、分类梳理、Onboarding 场景推荐骨架、Presets 架构预留。功能行为零变更。
- **阶段二**：品牌化（名称/图标）+ Settings 视觉打磨 + 首个 CustomModule。
- **阶段三**：安装器整合自有语言包与品牌，发布流程（含与官方版本的共存/迁移说明）。
