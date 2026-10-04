# PowerToys Cuin 开发交接文档（HANDOFF）

> **用途**：下一位开发者或 AI Agent 仅凭本文档 + 代码库即可继续开发，无需阅读历史聊天记录。
> **基线**：v0.1.0-preview.3 已发布（2026-10-04）；分支 `cuin-dev`，HEAD `7c08d7740`，工作区干净。
> **写法约定**：所有路径/类名/行为均经实际核查；凡未动态验证、仅静态推断的内容以 ⚠️ 标注。
> 配套文档：`docs/RELEASE_CHECKLIST.md`（发布 runbook 全文+历史）、`docs/CUSTOM_MODULE_GUIDE.md`、`docs/SYNC_GUIDE.md`、`docs/CODE_SIGNING.md`、`docs/IDENTITY_MAP.md`。

---

## 1. 项目目标与当前状态

**是什么**：[microsoft/PowerToys](https://github.com/microsoft/PowerToys)（约 0.101 train，.NET 10 / C++ / WinUI3）的社区二开发行版 **PowerToys Cuin (Community Edition)**，三大价值：

1. **设置界面全量中文化**（上游新设置 UI 无官方中文）+ 中/EN 一键切换；
2. **自有模块 CuinQuickActions**（Ctrl+Alt+Q 呼出 12 项系统快捷操作）；
3. 独立发行身份（与官方互斥安装）、Presets/OOBE 预设、双语 README。

**仓库**：`https://github.com/yangxijia111/PowerToys`（fork）。remote：`origin`=fork，`upstream`=microsoft。
**分支策略**：开发一律在 `cuin-dev`；`main` 冻结跟随上游（**禁止在 main 开发**，当前停在 `dd65f4017`）；`feature/*` 分支会触发 CI。

**完成度**：可安装、可升级、可发布的完整产品。三个公开版本（均为 Pre-release）：

| 版本 | tag | 状态 |
|---|---|---|
| v0.1.0-preview.1 | `0fe746ea4` | 已废弃（EXE bal:Condition 语法缺陷，仅 MSI 可用） |
| v0.1.0-preview.2 | `fbdd93399` | 已被 preview.3 取代 |
| v0.1.0-preview.3 | `a4e860aa9` | **当前版本**（2026-10-04 发布，升级 Gate 全过） |

**可运行状态**：CI（Cuin CI + Spell）双绿；安装器三件套（EXE/perUser MSI/perMachine MSI）由 `cuin-release.yml` 产出；真机升级链（preview.2→3）验证通过；开发机上装有 preview.3 perUser（`%LOCALAPPDATA%\PowerToysCuin`）。

---

## 2. 项目结构与核心文件地图

```
src/
├─ Version.props                     # ★ 版本单一来源（见 §4.5）
├─ runner/                           # C++ 主进程（PowerToys.exe：托盘/模块加载/热键/IPC server）
│   └─ settings_window.cpp:285       #   IPC "language" 分支（写 language.json，见 §8 已知问题 L1）
│   └─ UpdateUtils.cpp:120           #   FORK_AUTO_UPDATE_INSTALL_ENABLED = false（自动更新禁用）
├─ settings-ui/
│   ├─ Settings.UI/SettingsXAML/
│   │   ├─ Views/ShellPage.xaml(.cs) #   导航壳 + 页脚语言切换（:315-368 [fork-i18n]）
│   │   └─ Strings/{en-us,zh-CN}/Resources.resw  # ★ 主资源文件（~2057 键）
│   ├─ Settings.UI.Controls/Strings/ #   控件库资源（同样双语言）
│   └─ Settings.UI.Library/
│       ├─ LanguageModel.cs:14       #   读 \Microsoft\PowerToys\language.json（⚠️ 见 §8 L1）
│       └─ OutGoingLanguageSettings.cs # {"language":"xx"} IPC 消息体
├─ common/
│   ├─ ManagedCommon/
│   │   ├─ Branding.cs:19            # [fork-brand] 品牌常量（ForkName/ForkAppDataFolderName…）
│   │   └─ LanguageHelper.cs:15      #   各模块 App 构造消费；读 \Microsoft\PowerToys\language.json
│   ├─ interop/shared_constants.h:16 # ★ APPDATA_PATH="PowerToysCuin"（C++ 数据根唯一来源）
│   │                       :196-199 #   QA 显示/退出命名事件（与 C# CuinConstants.cs 对偶）
│   └─ SettingsAPI/settings_helpers.cpp  # get_root_save_folder_location → %LOCALAPPDATA%\PowerToysCuin
└─ modules/
    └─ cuin/                         # ★ fork 自有模块（Peek 模型，三个项目）
        ├─ cuinquickactions/         #   C++ 壳（dllmain.cpp，见 §4.3）
        ├─ CuinQuickActions.Common/  #   纯逻辑层（Actions/ 目录全部业务）
        └─ CuinQuickActions.UI/      #   WinUI3 面板（QuickActionsXAML/ + ViewModels/）
installer/PowerToysSetupVNext/       # ★ WiX 安装器（见 §4.4）
├─ Product.wxs:23-63,142             #   MajorUpgrade/REINSTALLMODE/ForceReinstallModeAmus
├─ Common.wxi:47,58                  #   UpgradeCode（perUser DA33EB25…/perMachine 78975C14…）
├─ PowerToysInstallerVNext.wixproj:11-17  # MsiVersion 编码 preview 序号
├─ componentGuidMap.psd1             #   文件组件 GUID 稳定化映射表（291 行）
└─ generateAllFileComponents.ps1 / generateMonacoWxs.ps1  # 确定性文件清单/GUID 生成
tools/
├─ check_fork_identity.py            # ★ CI 静态检查族（用法见 §6）
├─ check_upgrade_chain.py            #   升级链断言（10 项）
├─ check_release_assets.py           #   资产命名/版本一致性
├─ check_zh_cn_coverage.py           #   中文化覆盖率
├─ check_cuin_modules.py             #   QA 模块接线（事件名/slnx/installer/CI 步骤）
├─ check_sparse_registration.py      #   sparse MSIX 注册降级路径
└─ fork_sign_assets.ps1              #   签名两阶段脚本（拿到证书后启用）
.github/workflows/cuin-ci.yml        # push 触发：静态检查+构建+三组单测
.github/workflows/cuin-release.yml   # dispatch/v* tag 触发：完整安装器（只产 artifact 不发布）
.github/workflows/spelling2.yml      # check-spelling（only_check_changed_files）
.github/actions/vcpkg-restore/       # composite action：vcpkg 预装+binary cache（防 MSB3077 假失败）
.github/actions/spell-check/         # expect.txt 词典 + patterns.txt
docs/                                # 全部 fork 文档（§9 文档地图）
i18n_tools/（★在仓库外工作区上级）   # 翻译工具链：extract_missing.py / merge_resw.py / TRANSLATE_RULES.md
```

fork 自有改动全部带 `[fork-brand]` / `[fork-identity]` / `[fork-feature]` / `[fork-i18n]` 锚点注释——`git grep "fork-"` 可枚举全部改动面（上游同步时的冲突清单来源）。

---

## 3. 关键机制详解

### 3.1 发行身份隔离（动安装器前必读）

fork 与官方 PowerToys **互斥安装、不可共存**（COM CLSID/协议/AUMID/计划任务保留官方值，注定无法并存）：

| 项 | 值 | 定义处 |
|---|---|---|
| 安装目录 | `%LOCALAPPDATA%\PowerToysCuin`（perUser） | Product.wxs |
| C++ 数据根 | `APPDATA_PATH = L"PowerToysCuin"` | `shared_constants.h:16`（唯一来源） |
| UpgradeCode perUser | `DA33EB25-63A5-4E9A-8B04-D8AE81BB888D` | `Common.wxi:47` |
| UpgradeCode perMachine | `78975C14-0AA0-41A7-99C2-55F44200A919` | `Common.wxi:58` |
| Bundle UpgradeCode | `0A739D71-…`（完整值见 IDENTITY_MAP.md） | Bootstrapper 工程 |
| 品牌常量 | `ForkName="PowerToys Cuin"` 等 | `Branding.cs:19` |

- 官方在场装 fork → MSI Launch Condition 1603 阻止；fork 在场装官方 → runner 同名 mutex 互斥 + toast。
- 自动更新禁用：`UpdateUtils.cpp:120` `FORK_AUTO_UPDATE_INSTALL_ENABLED=false`（worker 不启动，:271）。
- **测试机卸载**：用 Bundle EXE `-uninstall`（msiexec 卸 MSI 不清 bundle 注册）；ARP 幽灵键用 `WindowsInstaller.Installer.RelatedProducts(UpgradeCode)` 枚举真 ProductCode 逐个 `/x`。

### 3.2 中文化体系与语言数据流

**资源层**：`Settings.UI/Strings/{en-us,zh-CN}/Resources.resw` 为双语言主资源；中文以 ZH-CN 限定符**嵌在主 PRI 内交付**（不是 zh-CN 卫星目录）。覆盖率由 `tools/check_zh_cn_coverage.py` 强制（CI），当前 2057/2057 键 100%（⚠️ 脚本口径为权威；直接 `grep "data name"` 会因格式差异得到 2058/2062，勿据此判断缺失）。

**翻译工具链**（`i18n_tools/`，位于仓库外的工作区上级，**不在 git 内**）：`extract_missing.py` 提取缺失键 → 分批翻译（人工或 LLM）→ `merge_resw.py` 校验合并（占位符 `{N}`/`%s` 数量、`\r\n`、XML 转义、键集合）。术语表 `TRANSLATE_RULES.md` 固定译名：保持唤醒/窗口分区/取色器/速览/批量重命名/文字提取（OCR）等。

**en-us resw 修改纪律**：新键做**文本级插入**，勿整文件重写（会丢 ResX 头注释 + 放大与上游 diff，同步时必炸）。

**运行时语言链路**（设计意图，⚠️ 当前存在断裂，见 §8 L1）：

```
Settings 页脚按钮（ShellPage.xaml.cs:349-368）
  → 读当前语言：LanguageModel.LoadSetting() 读 %LOCALAPPDATA%\Microsoft\PowerToys\language.json
    （不存在→跟随系统 UI 语言：zh* → zh-CN，否则 en-US）
  → 点击：SendDefaultIPCMessage(OutGoingLanguageSettings{"language":"en-US"})   [GeneralViewModel.cs:1266 常规下拉同款]
  → runner settings_window.cpp:285 "language" 分支写 get_root_save_folder_location()\language.json
    = %LOCALAPPDATA%\PowerToysCuin\language.json          ← ⚠️ 与 C# 读取路径不一致
  → ContentDialog 提示重启 → "立即重启" = ActionMessage("restart_maintain_elevation")
  → 重启后各 WinUI3 模块 App 构造函数调 LanguageHelper.LoadLanguage()
    → ApplicationLanguages.PrimaryLanguageOverride / CurrentUICulture 生效
```

**各模块消费点**：17 个 WinUI3 模块的 `App.xaml.cs` 构造函数统一走 `LanguageHelper.LoadLanguage()`（上游已有机制）。

### 3.3 CuinQuickActions 模块（Peek 模型）

三个项目，职责严格分离：

**C++ 壳**（`src/modules/cuin/cuinquickactions/dllmain.cpp`，全部逻辑 357 行，刻意保持最小）：
- `class CuinQuickActions : PowertoyModuleIface`；`MODULE_NAME = L"CuinQuickActions"`（= Settings enabled key = 数据目录名 `%LOCALAPPDATA%\PowerToysCuin\CuinQuickActions`）。
- `enable()` 启动 UI 进程（提权 runner 下强制 `RunNonElevatedFailsafe` 以普通权限运行——最小权限原则）；`disable()` 先 `SetEvent(TerminateEvent)` 优雅退出，1.5s 超时才 `TerminateProcess`（禁用态零进程）。
- `on_hotkey()`：面板未运行则 `launch_process()`，然后 `SetEvent(ShowEvent)`。
- 命名事件（`shared_constants.h:199-200` 定义，C# 侧 `CuinConstants.cs` 保存同名字符串；**字面量双侧一致由 `check_cuin_modules.py` 校验**）：
  - Show：`Local\PowerToysCuin-QuickActions-ShowEvent-4f3a9c2e-…`
  - Terminate：`Local\PowerToysCuin-QuickActions-TerminateEvent-9e5f2a7c-…`
- **`is_enabled_by_default()` 必须 override 返回 `false`**（`dllmain.cpp:312`）——基类默认 true 会与 Settings C# 侧"默认关闭"矛盾，出现"开关显示关但模块在跑"（preview.3 升级链真机发现）。改默认值时三处同步：dllmain override / Settings C# 默认 / settings.json 无键语义。
- 热键默认 Ctrl+Alt+Q（settings 损坏时兜底，绝不让 runner 崩）。

**纯逻辑层**（`CuinQuickActions.Common/Actions/`，全部可单测）：
- `ActionCatalog.cs`：12 动作静态编译期目录。安全设计：只调 Windows 已有页面/程序（`ms-settings:` URI + 固定 exe），无自由文本输入。`QuickActionKind` 枚举 7 种（LaunchUri/LaunchShellTarget/LockWorkStation/ClearClipboard/CloseForegroundApplication/RestartExplorer/OpenPowerToysSettings…）。
- `QuickActionRunner.cs`：执行器入口；`IsDestructive` 动作必须过确认闸。
- `ForegroundProcessGuard.cs`：系统进程保护名单（防误杀关键进程）。

**WinUI3 面板**（`CuinQuickActions.UI/`）：`MainWindow.xaml(.cs)` + MVVM（`MainWindowViewModel`/`QuickActionGroupViewModel`/`QuickActionItemViewModel`）+ `WindowsQuickActionExecutor.cs`（实现 Common 的 `IQuickActionExecutor`）+ `NativeEventWaiter.cs`（等待 Show/Terminate 事件）。

**WinUI3 unpackaged 模块四大坑**（新面板类模块通用，都是付过学费的）：
1. 资源名是 `AccentFillColorDefaultBrush`（没有 `AccentFillDefaultBrush`——用错 = 窗口创建即 XamlParse 崩 0xc000027b，绕过 UnhandledException，只能靠落盘诊断定位）；
2. `new Windows.ApplicationModel.Resources.ResourceLoader()` 在 unpackaged 抛 FileNotFound → 用 WinAppSDK 的 `ResourceLoader("模块.pri")` 显式指定；
3. **Window 根级与 DataTemplate 内 x:Bind 均不可用** → 经典 Binding + DataContext；DataTemplate 内 `Click="handler"` 在 Window 根上**静默失效** → 必须 `ListView.IsItemClickEnabled + ItemClick`（P0 教训）；
4. 面板失焦 `Close()` → 进程退出是**设计内**（Peek 同款），下次热键 runner 重新拉起，勿误判崩溃。

新模块开发完整指南：**`docs/CUSTOM_MODULE_GUIDE.md`**（含 Installer Gate：Full Build 成功 ≠ 安装器包含模块——必须过 `check_cuin_modules.py` + MSI File 表验证）。

### 3.4 安装器与升级链（WiX vNext）

**版本编码**（`PowerToysInstallerVNext.wixproj:11-17`）：`MsiVersion = $(Version).$(VersionPreview)` = `0.1.0.3`；`Product.wxs:23` Package 引用；Bundle 同版本。**preview 序号必须编入第四位**，三层（Bundle/MSI ProductVersion/FileVersion）都递增，否则升级被拒或文件不替换。

**升级链核心**（`Product.wxs`，动它前先读 :23-63 的注释块——那是一整段缺陷史）：
- `Property REINSTALLMODE=amus`（:54）+ **`ForceReinstallModeAmus` immediate CA 在 `CostInitialize` 前恢复 amus**（:63,:142）——因为 **Burn 引擎对升级 MSI 命令行强制传 `REINSTALLMODE=muso`** 覆盖 Property 表；'o' 语义下内容相同的无版本文件被 costing 跳过，而 MajorUpgrade 默认 Schedule（afterInstallValidate）的 RemoveExistingProducts 先删旧文件 → 升级 exit 0 但丢 1600+ 文件（preview.3 R2 真机 P0）。
- **MajorUpgrade 必须保持默认 Schedule**——`afterInstallExecute` 方案被实测否决（REP 在 InstallFiles 之后按旧 File 表删同路径新装文件）。
- bal:Condition（Bootstrapper）语法：操作符必须小写 `and`/`or`（与 MSI Condition 相反）；版本字面量必须带引号 `>= "0.0.0.0"`（preview.1 EXE exit 13 教训）。
- **文件组件 GUID 跨构建稳定化**：`componentGuidMap.psd1` 显式映射 + `generateAllFileComponents.ps1`/`generateMonacoWxs.ps1` 确定性生成（Monaco heat-harvest 曾每次构建变 GUID）。**新增 payload 文件后 GUID 必须进映射表**，否则升级文件对照不可预测。
- `check_upgrade_chain.py` 对以上全部有防回归断言（改安装器后必跑）。

**构建顺序**（CI 内，本地对齐 `build_preview2_local.cmd`）：主构建 → `generateDscManifests.ps1` → BugReportTool.sln / StylesReportTool.sln → publish.cmd → CmdPal → CA/BA（wcautil 需 shim csproj 全局布局还原）→ perMachine MSI → perUser MSI → Bootstrapper。**不可设 `IsPipeline=1`**（激活 21 语言 loc 块，fork 无上游 loc 产物）；清 prebuild 用 `/p:PreBuildEvent=`。

**验证 MSI 内容的纪律**：PowerShell `WindowsInstaller.Installer` COM 读表（Record 取值用 `StringData` 属性；`FROM Directory` 被拒要用 `SELECT * FROM Directory`）。**绝对禁止 `msiexec /a`**——对同 UpgradeCode 已装环境会 relocate/污染注册。

### 3.5 版本体系（单一来源 `src/Version.props`）

```xml
Version=0.1.0  VersionChannel=preview  VersionPreview=3
FileVersion = 0.1.0.3   （preview 序号编入第四位，防 Burn "Won't Overwrite; equal version"）
```
- 显示版本 `v0.1.0-preview.3`（About/OOBE）；MSI ProductVersion 与 FileVersion 都是 `0.1.0.3`。
- **升版改三处**：`Version.props` 的 `VersionPreview` → `check_release_assets.py` 的 workflow 期望清单 →（发布时）RELEASE_CHECKLIST 模板号。CI 有断言，漏改会红。
- **已知特性**：同版本覆盖安装不换二进制（MSI costing 跳过 equal version）——测试升级链必须跨 preview 序号。

### 3.6 CI/CD

| workflow | 触发 | 内容 |
|---|---|---|
| `cuin-ci.yml` | push cuin-dev / feature/* | ① Static checks（6 个 check_*.py）② 构建核心+runner+QA+Settings 单测 ③ 三组单测：Settings 335 / QA 54 / C++ 483（3 个提权 pipe 测试标注未执行——设计前提是提权 server，非回归） |
| `cuin-release.yml` | workflow_dispatch / push `v*` | 完整安装器三件套 → artifact `cuin-release-candidate`（EXE+双 MSI+SHA256SUMS.txt）。**只产 artifact，不创建 Release**（公开发布须人工，见 RELEASE_CHECKLIST §8-9） |
| `spelling2.yml` | push | check-spelling，只查改动文件；新词进 `.github/actions/spell-check/expect.txt`（禁 CamelCase 复合词，如 Cuin 已覆盖则 PowerToysCuin 不需要单独条目） |

- `vcpkg-restore` composite action：与 MSBuild integration **完全同参数**的预 install（manifest/triplet **含结尾反斜杠**）+ 有限重试 + `%LOCALAPPDATA%\vcpkg\archives` binary cache。背景：mirror.msys2.org 瞬时失败会被 MSBuild Exec 解析成 MSB3077 假失败。
- 资产下载坑：`gh run download` 会挂死 → `curl` 拿 redirect_url 后 16 段 Range 并行 + 校验每段完整（工作区 `download_tag_p3.sh` 是现成模板）。

---

## 4. 环境与构建

**开发机要求**：Windows x64 + VS2026 Community（实例 `D:\Develop\VS_2026`）+ .NET 10 SDK + Windows SDK 26100。
- UWP/C++ 组件必须用 vsconfig **原始 ID**（`Microsoft.VisualStudio.ComponentGroup.UWP.VC` 等）；Community 上缩写 ID 是 Non-installable（死路，已验证）。
- pwsh 依赖用 shim：`D:\Develop\pwsh-shim\pwsh.cmd` 转发 powershell.exe（PATH 前置即可，无需装 PS7）。

**本地全量构建**（cmd，27.7GB 内存机器的稳定参数）：
```bat
call "D:\Develop\VS_2026\Common7\Tools\VsDevCmd.bat" -arch=x64
set "PATH=%LOCALAPPDATA%\Microsoft\WindowsApps;D:\Develop\pwsh-shim;%PATH%"
set NUGET_HTTP_CACHE_TIMEOUT=1800000
set _CL_=/MP2
msbuild PowerToys.slnx -restore -p:Configuration=Release -p:Platform=x64 -m:2 -v:m
```
- `/MP4` 会 OOM（XAML 巨型生成文件）；`/m`（全节点）在用户会话内存紧张时 OOM → `/m:2` + `MSBUILDDISABLENODEREUSE=1`。
- 产物 ~90GB（bin/obj 75G + x64 16G）。**构建前向负责人确认磁盘余量**（用户磁盘极度敏感，有爆盘史）。
- msbuild 参数从 bash/cmd 传递时用 `-` 前缀（MSYS 会把 `/` 转路径）。
- **dev runner 坑**：构建 runner.vcxproj 只更新 `src/runner/x64/Release/`，dev 布局跑的是仓库根 `x64\Release\PowerToys.exe`——**必须手动同步**，否则模块"settings 有 key 但不出现"。
- 产物目录被运行中进程锁定报 MSB3027：先 taskkill dev runner 全家（QuickAccess/AdvancedPaste 等子进程也锁 WinUI3Apps）。

---

## 5. 测试与验证方法

**静态检查（改安装器/模块/版本/资源后必跑，全族进 CI）**：
```bash
python tools/check_fork_identity.py          # 发行身份（UpgradeCode/目录/AppData）
python tools/check_upgrade_chain.py          # 升级链 10 项断言
python tools/check_release_assets.py         # 资产命名与版本
python tools/check_zh_cn_coverage.py --fail-on-orphan   # 中文化覆盖率
python tools/check_cuin_modules.py           # QA 模块接线（事件名/slnx/installer/CI）
python tools/check_sparse_registration.py    # sparse MSIX 降级路径
```

**单元测试**（vstest 需 vswhere 定位，CI 里的写法可直接抄）：
```bash
vstest.console.exe Release\x64\tests\SettingsTests\net10.0-windows10.0.26100.0\Settings.UI.UnitTests.dll
vstest.console.exe Release\x64\tests\CuinQuickActions.Tests\PowerToys.CuinQuickActions.UnitTests.dll
# C++: Common.Utils.UnitTests（3 个提权 pipe 测试本地必失败，非回归）
```
本机 vstest：`D:\Develop\VS_2026\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe`。

**真机验证方法论**（锁屏/注入限制下踩出的可靠组合）：
- 点击 WinUI 卡片：CUA click；标准对话框按钮：PowerShell UIA Invoke；显面板：`SetEvent` 命名事件；等价 relaunch：直启 exe。CUA `elements()` 与 `click()` 之间不能插耗时调用（UIA 树漂移）。
- **不可自动化**（留 MANUAL GATE，每版负责人手测）：真实键盘热键（Ctrl+Alt+Q）、锁屏动作（注入被锁屏会话拒绝，设计如此）。
- **双 settings 根**：runner 真源 = `%LOCALAPPDATA%\PowerToysCuin\settings.json`（IPC 写入、模块开关在此）；Settings C# 的 EnabledModules 落在 `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`。验证"设置保留"认准 runner 真源；历史遗留的两边键不一致（如 Shortcut Guide runner=false / C#=true）是测试期脱钩，非升级缺陷。
- 升级验证文件完整性：**以 MSI File 表为期望清单**（Directory/Component/File 三表联查生成路径，diff 安装目录，缺失必须为 0），不要拿"升级前文件数"做基线（含测试遗留）。参考实现：工作区 `verify_filetable.ps1` + `diff_filelist.py`。
- runner 行为判读：启动自重启换 PID（按名+Path 判存活）；升级后检测磁盘版本≠运行版本会弹 toast 退出（正常）；orphan 子进程 Path 为空按 Name 抓。
- 静默安装：PowerShell `Start-Process -ArgumentList '-install','-quiet'`（Git Bash 裸转参会弹 UI 卡住）。
- PS5.1 坑：`Set-Content -Encoding UTF8` 带 BOM → runner `json::from_file` 解析失败回退全模块默认启用；无三元运算符；**含中文的 .ps1 必须 UTF-8 BOM 保存**（无 BOM 时 GBK 代码页解析直接语法错误）。

**发布 Gate 全流程**：`docs/RELEASE_CHECKLIST.md`（§4 升级 Gate 清单 + §5 MANUAL GATE + §7-10 发布动作；preview.3 的完整勾选记录是最新范例）。

---

## 6. 重要设计决策与理由

1. **发行身份隔离而非共存**：官方与 fork 的 COM CLSID/协议/AUMID 相同，技术上无法并存 → 选择互斥安装（1603 阻止 + 互斥 toast），换来零身份冲突与干净的升级链。代价：迁移用户必须卸一方。
2. **保留官方 GUID 值 + fork 自有 UpgradeCode/目录**：COM 层保持与上游最大相似度（降低同步成本），发行层（UpgradeCode/安装目录/AppData/更新端点）完全独立。见 IDENTITY_AUDIT.md。
3. **自动更新禁用**：fork 无代码签名与更新信任链，强推更新风险大 → `FORK_AUTO_UPDATE_INSTALL_ENABLED=false`，用户手动下载覆盖升级（升级保留全部配置已验证）。
4. **QA 模块用 Peek 三层模型**（C++ 壳 + 纯逻辑 Common + WinUI3 UI）：C++ 壳保持最小（357 行）让 runner 稳；业务全在可单测的 Common；UI 隔离在独立进程（崩了不连累 runner）。事件名双侧字面量由静态检查锁定。
5. **QA 默认关闭且三处同步**（dllmain override false / C# 默认 off / 无键语义）：preview.3 升级链曾因基类默认 true 出现"显示关实际跑"的状态漂移（3546179d3 修复）。
6. **中文嵌主 PRI 而非卫星目录**：与上游资源管线一致，安装器无需额外语言目录；代价是 resw 全量进主 PRI（体积可忽略）。
7. **MsiVersion/FileVersion 编码 preview 序号**：MSI 版本比较只看四段数字；preview 系列必须让"旧 < 新"成立且文件版本递增，否则三层拒绝/不换文件（preview.1→2 两个缺陷的修复）。
8. **ForceReinstallModeAmus CA 而非改 Schedule**：Burn 强传 muso 是引擎行为改不了；afterInstallExecute 方案实测仍丢文件；恢复 amus + 默认 Schedule（REP 先删、InstallFiles 后全量装）是唯一经真机验证的组合。
9. **CI 静态检查族（Python）**：把"付过学费的坑"固化成断言（升级链 10 项/资产命名/身份/覆盖率/模块接线），防回归成本最低。

---

## 7. 已完成 / 未完成 / 技术债 / 风险

### 已完成（全部经真机或 CI 验证）
- zh-CN 设置界面 100%（2057/2057 键）+ 翻译工具链 + 覆盖率 CI 强制
- CuinQuickActions 模块全链（E2E 11/12 动作过，危险动作确认闸全过）
- 发行身份隔离（真机场景 A-D 过）、三套版本编码、安装器三件套 CI 产出
- 升级链三代缺陷修复（bal:Condition 语法 / 版本编码 / Burn REINSTALLMODE + GUID 稳定化）
- 三个公开版本；preview.3 升级 Gate：File 表 1845/1845 零缺失、配置全保留
- CI 稳定化（vcpkg-restore）、拼写降噪、签名基建（无证书待启用）
- 本交接文档 + RELEASE_CHECKLIST 全勾选记录

### 未完成 / 已知问题（按优先级）
- **L1（P1，⚠️ 静态分析判定、未动态复现）：语言切换写读路径断裂**。runner 写 `%LOCALAPPDATA%\PowerToysCuin\language.json`（settings_window.cpp:285 → get_root_save_folder_location），但 `LanguageModel.cs:14` 与 `LanguageHelper.cs:15` 硬编码读 `\Microsoft\PowerToys\language.json`。fork 改 C++ 数据根后此上游链路写读不对称 → **页脚一键切换与常规语言下拉点击后重启，语言大概率不变**（写入的文件 C# 永远读不到）。真机两处文件均不存在（默认跟随系统 zh，功能未被动过）故未暴露。**推荐修法（最小改动）**：`settings_window.cpp` 的 `language` 分支改为写 `%LOCALAPPDATA%\Microsoft\PowerToys\language.json`（保持 C# 读取侧不动，两处 C# 被 17+ 模块消费，动读侧风险大）；修复后需真机验证"切换→重启→语言生效"，并给 `check_fork_identity.py` 加路径一致性断言。
- L2：模块编辑器独立进程（FancyZonesEditor 等卫星 resx）未汉化（后续增量，走 i18n_tools 同流程）。
- L3：安装包未签名 → SmartScreen"未知发布者"；Win11 新式右键菜单不可用（sparse MSIX 未签名被拒，预期降级）。签名基建已就绪（`tools/fork_sign_assets.ps1` + cuin-release.yml 可选 secrets 门控步骤 + CODE_SIGNING.md），拿到证书即可启用，签名后 sparse MSIX 自动恢复。
- L4：从官方版本迁移到 fork 的说明文档（README Roadmap 未勾项）。
- L5：MANUAL GATE 两项每版需负责人手测：Ctrl+Alt+Q 真实热键、锁屏动作。

### 技术债 / 风险
- **上游漂移**：microsoft/PowerToys main 持续演进，同步窗口越大冲突越痛。热点：ShellPage.xaml > en-us resw > [fork-*] 身份文件 > ModuleHelper/EnabledModules > App.xaml.cs > GeneralPage（中文化）。预案：SYNC_GUIDE.md。
- **双 settings 根**（PowerToysCuin vs Microsoft\PowerToys）是历史设计（C# 侧大量硬编码），短期不做统一（改动面大），所有设置相关验证都要意识到有两份。
- 27.7GB 内存机器上本地全量构建紧贴 OOM 边界（参数见 §4）；GH runner 构建约 1h05m。
- `.wxs.bk` 备份文件是构建事件产物（安装器 prep 会消耗/恢复 .bk），不是垃圾；`MonacoSRC.wxs` 是 gitignore 生成物（由 generateMonacoWxs.ps1 产出）。

---

## 8. 下一步推荐开发顺序

1. **修 L1 语言切换路径断裂**（半天：改 settings_window.cpp 一个分支 + check 断言 + 真机验证切换生效）——这是已发布功能性缺陷，最优先。
2. L2 模块编辑器汉化增量（i18n_tools 流程现成，纯执行）。
3. 获取代码签名证书 → 按 CODE_SIGNING.md §5/§6 接入（解锁 SmartScreen + Win11 新式菜单）。
4. 手动完成 preview.3 的两项 MANUAL GATE（热键/锁屏），沉淀为每版固定动作。
5. 上游同步窗口评估（如 upstream 有重要修复/安全更新，按 SYNC_GUIDE merge；重点回归：语言链、QA 接线、升级链静态检查族全绿）。
6. L4 迁移说明文档；下一版本（preview.4）走 RELEASE_CHECKLIST runbook。

---

## 9. 危险区：不可随意修改的代码/配置

| 位置 | 原因 |
|---|---|
| `Product.wxs` MajorUpgrade Schedule / ForceReinstallModeAmus / REINSTALLMODE | 三代升级缺陷的唯一正确组合；改任何一处需重走升级 Gate（丢文件 P0 复发风险） |
| `componentGuidMap.psd1` 已有 GUID | 升级文件对照依赖跨构建稳定；新 payload 必须新增条目而非改动已有 |
| `shared_constants.h` APPDATA_PATH / 两个 QA 事件名 | C++ 数据根唯一来源 / 与 C# CuinConstants.cs 对偶（静态检查锁定）；改名=数据迁移事故 |
| `Common.wxi` UpgradeCode 两枚 | 改了 = 与已发布版本断链，用户无法升级 |
| `Version.props` 版本编码规则（FileVersion 含 preview 序号） | 同版本/回退版本会导致升级被拒或文件不替换 |
| `src/runner/UpdateUtils.cpp` FORK_AUTO_UPDATE_INSTALL_ENABLED | 无签名信任链前开启 = 向用户推不可信二进制 |
| en-us resw 整文件重写 | 丢 ResX 头注释 + 放大上游 diff；只做文本级插入 |
| `.github/actions/spell-check/expect.txt` 的 CamelCase 复合词 | check-spelling 禁复合词条目（拆词入典） |
| 上游共享代码中的 `[fork-*]` 锚点注释 | 锚点丢失 = 上游同步时丢失 fork 改动意图标记 |
| MSI 内 Property 表的 COM CLSID/协议/AUMID（保留官方值） | 动了 = 与官方共存/互斥行为全部漂移 |

**通用纪律**：改安装器 → 跑 `check_upgrade_chain.py` + 真机升级 Gate；改模块 → `check_cuin_modules.py` + 单测；改资源 → `check_zh_cn_coverage.py`；动 CI → dispatch 一次 Cuin Release Build 验证全绿。

---

## 10. 临时方案与特殊约束（必读）

- **网络**：`github.com:443` 间歇阻断而 `api.github.com` 常通——git push/clone 失败先循环重试（25s 间隔）；gh API 用 unset 代理直连；artifact 下载用 curl+Range。本机历史代理 `127.0.0.1:7897` 不一定在线，先探端口。
- **临时脚本不入库**：工作区根（仓库外）有大量测试/验证脚本（upgrade_gate_p3.ps1、verify_filetable.ps1、download_tag_p3.sh、e2e_*.ps1 等）——**它们是工具不是产品**，其中记录了真机验证方法论（本文 §5 已提炼），可复用但勿提交进仓库。
- `D:\Develop\VS_2026_old`（旧失败实例）待删（删前向用户确认——用户对磁盘操作有"先报告获准"的硬要求）。
- Bash 内联 `python -c` 的反斜杠/反引号/中文会炸 → 一律写 .py 文件执行；Git Bash `/tmp` ≠ Windows 临时目录（跨进程传文件用真实路径）。
- msiexec 维护模式坑：对**已装同 ProductCode** 的 MSI `/i` 走维护模式不触发 Launch Condition（测阻止逻辑前先卸载）；msiexec /a 禁用（§3.4）。
- 构建要求 git 工作区干净（build-installer.ps1 脏则报错，-Force 跳过）；MIDL 会把 `App_h.h` 生成到源目录弄脏工作区（构建前删）。
- **用户硬约束**（来自项目所有者）：磁盘极度敏感——任何 >1GB 的写入/清理/重装前必须先报告并获批准。

---

## 11. 文档地图

| 文档 | 内容 |
|---|---|
| `docs/RELEASE_CHECKLIST.md` | 发布 runbook 全文 + 三版历史 + 排障记录（最厚实的经验库） |
| `docs/CUSTOM_MODULE_GUIDE.md` | 新模块开发全流程（Peek 模型）+ Installer Gate |
| `docs/SYNC_GUIDE.md` | 上游同步与冲突预案（§1.1 身份文件 / §1.2 模块接入） |
| `docs/CODE_SIGNING.md` | 签名架构与两模式手册 |
| `docs/IDENTITY_AUDIT.md` / `IDENTITY_MAP.md` | 身份审计与 GUID 对照表 |
| `docs/DEVELOPMENT_PLAN.md` / `POST_RELEASE_PLAN.md` | 阶段开发计划 |
| 仓库外 `i18n_tools/TRANSLATE_RULES.md` | 翻译术语表 |
| 仓库外 `release_notes_v*.md` | 各版双语 release notes 草稿 |
| 仓库外 `upgrade_gate_p3.ps1` / `verify_filetable.ps1` / `diff_filelist.py` | 真机升级 Gate / File 表 diff 现成实现 |

---

## 12. 接手人 Quick Start

1. `git checkout cuin-dev && git pull`；确认 Cuin CI 最近绿；
2. 装 VS2026（§4 组件要求）+ 磁盘预算 ~90GB（**先报告**）；
3. 跑 §5 静态检查全族确认基线；
4. 开发规范：上游 `AGENTS.md` + `doc/devdocs/`（fork 追加：注释/提交信息可中文；StyleCop 全局强制——三行版权头/一文件一类型/SA1515 空行规则，新文件批量处理用 python 按实际行尾处理）；
5. 第一件事建议做 §8 的 L1 修复（小、独立、验证路径清晰，顺便熟悉 IPC→runner→重启全链路）；
6. 发布任何版本必须完整走 RELEASE_CHECKLIST，不跳 Gate——历史上每个 Gate 都拦住过真实缺陷。

---

*本文档由 2026-10-04 交接时基于 cuin-dev@7c08d7740 实际核查编写。L1 问题为当日静态分析新发现，修复前请先独立确认。*
