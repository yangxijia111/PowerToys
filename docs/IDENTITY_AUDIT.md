# 发行身份审计（IDENTITY_AUDIT）

> 分支：`cuin-dev` · 审计日期：2026-09-27 · 状态：Phase 3
>
> 本文档盘点 PowerToys Cuin（Community Edition）fork 与官方 Microsoft PowerToys 之间**全部发行身份项**，
> 评估共存冲突，并给出四类处置结论。配套映射表见 [IDENTITY_MAP.md](IDENTITY_MAP.md)。
>
> 审计方法：通读 `installer/`、`src/runner/`、`src/common/`、`src/modules/`、`src/settings-ui/`、
> `src/PackageIdentity/`、DSC 与构建脚本，结合代码调用关系与安装行为分析，非仅凭名字判断。

## 0. 发行策略结论

**采用"互斥安装"：fork 安装器主动检测官方 PowerToys（两个官方 UpgradeCode），检测到即明确阻止安装。**

官方 PowerToys 的安装器检测不到 fork（官方只搜自己的 UpgradeCode），因此 fork 侧必须单向设防。
在此策略下：

- fork 自身的 MSI/Bundle/卸载/目录/快捷方式身份完全独立 —— fork 的安装、升级、卸载不再触碰官方；
- COM CLSID、协议、AUMID、计划任务等"系统单点"身份继续复用官方值 —— 由互斥安装保证任意时刻
  只有一个发行版持有它们，无需大规模改动上游代码（符合 Phase 3 "最小必要隔离"约束）；
- runner 单实例 Mutex 保留与官方同名 —— 这是违规并存场景（用户强行并装）下的最后安全带：
  同一会话仍只能运行一个 runner，后启动者自动退出并把设置命令转发给先启动者，
  避免注册表/事件全面拉锯。

### 为什么不选"完全共存"（审计依据）

1. **sparse MSIX 无法安全分叉（P0 根因）**。PowerRename / ImageResizer / File Locksmith / NewPlus
   的 Win11 右键菜单 + `Microsoft.PowerToys.SparseApp` 包身份，全部以
   `Name+Publisher 完全相同` 的 MSIX 经 `AddPackageByUriAsync(ForceUpdateFromAnyVersion=true)` 注册，
   后注册者整体接管（外部内容指向自己的安装目录）。fork 若改身份 Name/Publisher，就必须有对应的
   受信代码签名链 —— fork 拿不到微软签名，自签证书要求最终用户手工信任根证书，不可行。
   共存下 Win11 扩展必然互抢，无法隔离。
2. **COM/Shell 扩展是"启动即抢注"**。4 个经典菜单 CLSID + 13 组 preview/thumbnail handler CLSID
   与官方逐字相同，`shell_ext_registration.h` 的 repair 逻辑保证每次启动都会把
   `InprocServer32` 改写回自己，且一方 disable 会删除另一方依赖的共享键。共存隔离需要横跨
   20+ 文件改动 17+ 个 GUID 与注册表键名，冲突热点暴增，违背"不做大规模重构"约束。
3. **全局单点太多**。`powertoys://` 协议、Toast Activator CLSID、AUMID `Microsoft.PowerToysWin32`、
   托盘窗口类、计划任务 `\PowerToys`、`Software\Classes\powertoys` 与 `Software\Microsoft\PowerToys`
   键树 —— 共存要求全部修改，任何遗漏都会串线（通知互相删除、自启互相覆盖、更新协议拉起对方）。
4. 相反，互斥安装下以上全部保持安全：系统里同一时刻只有一套 PowerToys 系发行版持有这些身份。

## 1. 必须隔离（本阶段已修改）

| # | 项目 | 官方值 | fork 新值 | 位置 | 说明 |
|---|---|---|---|---|---|
| 1 | MSI UpgradeCode（perMachine） | `42B84BF7-5FBF-473B-9C8B-049DC16F7708` | `78975C14-0AA0-41A7-99C2-55F44200A919` | `installer/PowerToysSetupVNext/Common.wxi:54` | 原 fork MSI 是官方的"同产品升级"，安装即原地替换官方。独立后 fork 与官方 MSI 层完全互不相认 |
| 2 | MSI UpgradeCode（perUser） | `D8B559DB-4C98-487A-A33F-50A8EEE42726` | `DA33EB25-63A5-4E9A-8B04-D8AE81BB888D` | `Common.wxi:45` | 同上（用户级安装链） |
| 3 | Bundle UpgradeCode | `6341382d-c0a9-4238-9188-be9607e3fab2` | `0A739D71-3045-42E6-8505-9DB32432F8E6` | `PowerToys.wxs:1` | 原引导器与官方引导器互相视为升级；独立后 fork 装卸不再被官方引导器接管，反之亦然 |
| 4 | Bundle Name / Manufacturer | `PowerToys (Preview) x64` / `Microsoft Corporation` | `PowerToys Cuin (Community Edition) x64` / `PowerToys Cuin Community` | `PowerToys.wxs:7` | ARP 卸载列表名（引导器安装时主要显示这条）与发行者，避免被误认为微软官方 |
| 5 | MSI Name / Manufacturer | `PowerToys (Preview)` / `Microsoft Corporation` | `PowerToys Cuin (Community Edition)` / `PowerToys Cuin Community` | `Product.wxs:20` | Name 已在 Phase 2 品牌化；Manufacturer 本阶段从 Microsoft 改为社区发行者（ARP Publisher、`Software\[Manufacturer]` 键随之独立） |
| 6 | 安装目录 | `...\PowerToys` | `...\PowerToysCuin` | `Product.wxs:292`、`PowerToys.wxs:24,26`、`Product.wxs:102`（perUser=`%LOCALAPPDATA%\PowerToysCuin`，perMachine=`Program Files\PowerToysCuin`） | 文件系统层隔离；即使并装也不会文件互踩 |
| 7 | 开始菜单/桌面快捷方式名 | `PowerToys (Preview)` | `PowerToys Cuin` | `Core.wxs:101,115` | 修复"文件夹叫 Cuin、快捷方式叫 PowerToys (Preview)"的可见面不一致 |
| 8 | AppData 设置/日志根 | `%LOCALAPPDATA%\Microsoft\PowerToys` | `%LOCALAPPDATA%\PowerToysCuin` | `src/common/interop/shared_constants.h:13`（`APPDATA_PATH`）、`src/common/SettingsAPI/settings_helpers.cpp`（两处收敛引用该常量）、`SettingPath.cs`、`Helper.cs`、`SettingsConfigHelper.cs` | settings.json、模块配置、RunnerLogs、UpdateLogs、Updates、UpdateState.json、ConfigBackup、oobe/last_version 全部随根隔离。**不迁移、不删除官方数据** |
| 9 | LocalLow 日志根 | `%LOCALAPPDATA%Low\Microsoft\PowerToys` | `%LOCALAPPDATA%Low\PowerToysCuin` | `settings_helpers.cpp` | 预览处理器 LocalLow 日志 |
| 10 | 更新检测端点 | `api.github.com/repos/microsoft/PowerToys/...` | `api.github.com/repos/yangxijia111/PowerToys/...` | `src/common/updating/updating.cpp:18-19` | 防止未来版本号离开 0.0.x 后自动下载**官方**安装包并执行（上游信任链只认微软签名，fork 包会被拒 —— 安全失败，见 §4-1） |
| 11 | 运行时 MSI 身份常量 | 官方 UpgradeCode ×2 + 旧组件 GUID `A2C66D91-...` | fork UpgradeCode ×2 + `30261594-41A6-4509-AD09-FBC4E692F441` | `src/common/utils/MsiUtils.h:14-16`、`installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp:44-45` | `DetectPrevInstallPathCA` 用它探测 fork 自己的旧版路径；组件 GUID 同步为 fork `powertoys_exe` 组件（修复上游旧 WiX3 GUID 对不上本 fork MSI 的既有失配） |
| 12 | DSC 卸载项检测名 | `DisplayName -eq "PowerToys (Preview)"` | `"PowerToys Cuin (Community Edition)"` | `src/dsc/PowerToys.Settings.DSC.Schema.Generator/DSCGeneration.cs` ×4 | fork MSI DisplayName 改过之后 DSC 的 `GetPowerToysSettingsPath()` 检测已失配，本阶段修复 |
| 13 | 更新引导器身份锚 | 产品名前缀 `PowerToys (Preview)` | `PowerToys Cuin (Community Edition)` | `src/common/updating/installer.cpp`（`POWERTOYS_PRODUCT_NAME_PREFIX`） | 与新 Bundle Name 保持一致（该常量用于验证下载的引导器版本资源） |
| 14 | pipe 客户端签名要求 | `requireMicrosoftSignature = true` | `false`（保留目录+basename+版本三重校验） | `src/runner/settings_window.cpp`、`src/runner/quick_access_host.cpp` | 上游新引入的 pipe 鉴权在 Release 下要求调用方（Settings/QuickAccess exe）持微软签名 —— fork 构建永远无法满足，Settings IPC 会被 runner 拒绝。这是 fork 自身可用性阻断，必须关闭签名项；目录/进程名/版本校验保留 |

## 2. 建议隔离（本阶段不动，后续 Release 前处理）

| 项目 | 当前值/位置 | 说明 |
|---|---|---|
| 引导器窗口 logo | `installer/PowerToysSetupVNext/Images/logo44.png`（`PowerToys.wxs:11`） | 仍是微软官方图；安装 UI 可见面。纯品牌资产，不影响身份安全，建议随 Binary Preview 前替换 |
| MSI 产物文件名 | `PowerToysSetup/PowerToysUserSetup-<ver>-x64.exe/.msi`（两个 wixproj） | 与官方产物同名，容易混淆但只是文件名。建议未来改为 `PowerToysCuinSetup-*`（改名会牵动 CI 脚本与更新资产名匹配 `updating.h:36-37`，需整体设计） |
| TerminateProcessesCA 进程名名单 | `CustomAction.cpp:1576+`（`PowerToys.exe`、`PowerToys.*.exe` 等） | 进程名与官方相同。互斥安装下 kill 的必然是本 fork 自身进程，安全；若未来开放共存需重审 |
| 安装遥测 ETW provider | `PowerToys_Installer`（`CustomAction.cpp:59`）、runner `Microsoft.PowerToys`（`trace.cpp:8`） | 与官方同名 provider，仅诊断数据混淆级问题，无运行时冲突 |

## 3. 可以继续复用（互斥安装保证唯一持有者）

以下身份在"互斥安装"策略下**保持官方值**，不修改。共同依据：系统内同一时刻只有一个发行版
安装/运行，这些单点不会产生双向覆盖。

| 项目 | 值/位置 | 复用依据 |
|---|---|---|
| Runner 单实例 Mutex | `Local\PowerToys_Runner_MSI_InstanceMutex`（`appMutex.h:13`） | ① 互斥安装保证不并存；② 违规并装时同名 mutex 是安全带：同会话只活一个 runner，后者自动退出（进程级对象，退出即释放，无持久残留） |
| 托盘窗口类 | `PToyTrayIconWindow`（`tray_icon.h:29`） | 同上；fork 的更新 Stage1 `FindWindow` 只会命中"当前活动的那个 runner" |
| 全部固定名事件 | `shared_constants.h`（`Local\PowerToys*` 系列约 60 个） | 进程级内核对象，进程退出即消失；互斥下无并发冲突 |
| KBM 引擎 mutex | `Local\PowerToys_KBMEngine_InstanceMutex`（`shared_constants.h:183`） | 同 runner mutex |
| Named Pipe | `\\.\pipe\powertoys_runner_<UUID>` / `powertoys_settings_` / `powertoys_quick_access_`（`settings_window.cpp:455-456`、`quick_access_host.cpp:141-142`） | 每次启动 UUID 后缀，天然不撞；鉴权按目录+进程名+版本（见 §1-14） |
| 经典菜单 CLSID 与注册键 | PowerRename `0440049F`、ImageResizer `51B4D7E5`、FileLocksmith `84D68575`、NewPlus `FF90D477` 及各自 `ContextMenuHandlers` 键（各模块 `RuntimeRegistration.h`） | 运行时 HKCU 注册 + 启动 repair；互斥下唯一持有者，官方与 fork 不同时存在 |
| Preview/Thumbnail handler | 13 组 CLSID（`FileExplorerDllExporter/dllmain.cpp:13-49`、`modulesRegistry.h`） | 同上（运行时 HKCU，`registry.h:454-537`） |
| Toast Activator CLSID | `{DD5CACDA-7C2E-4997-A62A-04A597B58F76}`（`Core.wxs:33`、`notifications.cpp:61`） | 注册表单点；互斥下唯一持有者 |
| AUMID | `Microsoft.PowerToysWin32`（`notifications.cpp:53`、`Core.wxs:102`） | toast 归组标识；互斥下不会互相删除通知 |
| `powertoys://` 协议 | `Software\Classes\powertoys`（`Core.wxs:39-51`、`main.cpp:67`） | 单点注册；互斥下 `powertoys://update_now/` 只会拉起当前唯一安装版 |
| 计划任务 | `\PowerToys\Autorun for <user>`（`auto_start_helper.cpp:103,125`；卸载 CA 删 `\PowerToys` 文件夹） | 官方卸载会删自己的同名任务；互斥下 fork 卸载时任务只可能属于 fork |
| 服务名 | `PowerToys.MWB.Service`（MouseWithoutBorders，`CustomAction.cpp:999`） | 同上 |
| 注册表 bookkeeping/sentinel | `Software\Classes\powertoys\components\*`（约 195 处）、`Software\Microsoft\PowerToys\*`（sentinel、模块运行时配置、AllowDataDiagnostics） | 互斥下唯一持有者；fork 卸载 CA 清理的是自己的键 |
| GPO 策略键 | `Software\Policies\PowerToys` | 企业策略语义，读写同一键无冲突 |
| PasswordVault 凭据前缀 | `PowerToys_AdvancedPaste_`（`CustomAction.cpp:854-932`） | 卸载清理自己的凭据 |
| DSC 模块路径 | `Microsoft.PowerToys.Configure`（PS Modules 目录） | 互斥下唯一持有者 |
| CmdPal 宿主包 | `Microsoft.CmdPal.UI` msix 注册（`CmdPal.wxs`、`CustomAction.cpp:1344-1382`） | 同 sparse 包（见 §4-2）；互斥下唯一 |
| ETW/遥测、版本资源 | provider 名、`version.h` CompanyName 等 | 无运行时冲突；品牌可见面（About/版权）Phase 2 已处理 |
| exe/进程名 | `PowerToys.exe`、`PowerToys.Settings.exe`、`PowerToys.*.exe` | 文件在独立目录（§1-6）内，不互踩；pipe 鉴权按 basename+目录校验仍自洽 |

## 4. 暂时不能安全修改（明确记录，不擅自改动）

| # | 项目 | 现状 | 为什么不能改 | 风险与后续 |
|---|---|---|---|---|
| 1 | 更新信任链（`verify_installer_trust`） | `installer.cpp`：WinVerifyTrust + 签名者 O=Microsoft + 官方 UpgradeCode 白名单 + 产品名前缀校验 | fork 无代码签名证书。放开校验 = 允许任意人向 fork 用户推任意 EXE 提权执行（不可接受）；保留校验 = fork 自更新永远"安全失败"（拒绝安装非微软签名包） | 当前版本 0.0.1 落在 `updating.cpp:86-90` 的 0.0.x guard 内，自动更新整条路径不激活，安全。**未来 bump 版本前必须**：fork 引入签名证书并重设计信任锚（新签名者 O、新 UpgradeCode 白名单）。端点已指向 fork repo（§1-10），不会误拉官方包 |
| 2 | sparse MSIX 身份 ×5 | `Microsoft.PowerToys.SparseApp`（`src/PackageIdentity/AppxManifest.xml`）+ 4 个 `Microsoft.PowerToys.*ContextMenu`（各模块 AppxManifest），Publisher 均为 `CN=Microsoft Corporation,...` | 改 Name/Publisher 需要受信签名链（同 §4-1）；且安装器 CA 硬编码 family `Microsoft.PowerToys.SparseApp_8wekyb3d8bbwe`（`CustomAction.cpp:661,740`），身份/签名/family 三者联动，牵一发动全身 | **已知限制**：fork（无微软签名）在最终用户机器上这 5 个 MSIX 注册会失败 —— `RegisterSparsePackage` 捕获异常记日志返回 false（`package.h:200-249`），模块照常启用，Win10 经典菜单键（HKCU）不受影响。互斥安装下也不存在与官方互抢的问题。Binary Preview 前需决策：接受 Win11 新菜单降级，或引入自签/签名方案 |
| 3 | 本机旧 fork 测试安装 | 用户本机 Phase 2 曾以**官方身份**（旧 UpgradeCode）装过 fork 0.0.1 测试版 | 新安装器会把它当作"官方 PowerToys"检测到并阻止安装（这正是新防护逻辑的预期行为） | 测试机操作项：先卸载旧测试安装，再验证新安装器 |
| 4 | MSVC 运行库 / WebView2 引导包 | 官方 WebView2 检测 GUID `{F3017226-...}`（`PowerToys.wxs:20-21`） | WebView2 是系统共享组件（微软资产），不应也不需要隔离 | 保留 |

## 5. 共存行为矩阵（互斥策略下的预期行为）

| 场景 | 预期行为 |
|---|---|
| 官方已装 → 安装 fork | **阻止**（Bundle bal:Condition + MSI Launch Condition 双层，文案明确提示先卸载官方）；官方无损 |
| fork 已装 → 安装官方 | 官方安装器检测不到 fork（只搜官方 UpgradeCode），**可能成功并装** —— 见 §6 剩余风险 |
| fork 已装 → 再装/升级 fork | MSI MajorUpgrade 正常移除旧版再装（同 UpgradeCode 升级链）；不同 scope（perUser/perMachine）互相阻止 |
| 卸载 fork | 只删 fork 目录（`PowerToysCuin`）、fork 注册表项、fork 计划任务；**不触碰** `%LOCALAPPDATA%\PowerToysCuin` 用户数据（与官方卸载行为一致）、不触碰官方任何数据（官方数据在 `Microsoft\PowerToys` 树，fork 代码不再读写） |
| 官方与 fork 同时运行 | 互斥下不存在（不能并装）。违规并装时同名 runner mutex 保证同会话只有一个 runner |

## 6. 剩余风险（如实记录）

1. **官方后装可覆盖 fork 的运行面**：官方安装器感知不到 fork。官方装上后：`Software\Classes\powertoys`
   协议、Toast CLSID、AUMID、sparse MSIX、shell 扩展注册都会被官方接管；两 runner 因同名 mutex
   只能活一个。fork 文件本身不受影响（目录不同、MSI 互不相认）。缓解：README 已声明不与官方混装；
   未来可在 fork runner 启动时检测官方安装并提示（未实施，避免扩大改动面）。
2. **官方卸载残留的 AppData 旧树**：官方卸载保留 `%LOCALAPPDATA%\Microsoft\PowerToys`（官方设计）。
   fork 已完全不读写该树，仅占用磁盘，无行为影响。
3. **fork 的 sparse MSIX 在用户机注册失败**（§4-2）：Win11 右键菜单（4 模块）降级为不可用，
   Win10 经典菜单与模块本体不受影响。
4. **合并上游时的身份热点**：`Common.wxi`、`PowerToys.wxs`、`Product.wxs`（头部）、`Core.wxs`
   （快捷方式）、`MsiUtils.h`、`CustomAction.cpp`（GUID 常量）、`shared_constants.h:13`、
   `settings_helpers.cpp`、`SettingPath.cs`、`updating.cpp:18-19`、`installer.cpp`（产品名锚）、
   `DSCGeneration.cs`、`SettingsConfigHelper.cs` —— 全部带 `[fork-identity]` 锚点，
   详见 [SYNC_GUIDE.md](SYNC_GUIDE.md)。

## 7. 验证方式

- 静态校验：`python tools/check_fork_identity.py`（GUID 唯一性、fork 身份值与 MAP 一致、
  官方 UpgradeCode 仅允许出现在"检测"位置）。
- 托管单测：`Settings.UI.UnitTests` 新增 `IdentityTests.cs`（AppData 根、Branding 常量、SettingPath 拼接）。
- 安装器场景测试（真机）：见 `docs/DEVELOPMENT_PLAN.md` Phase 3 节 —— A（fork 独立全流程）、
  B（官方→fork 阻止）、C（fork→官方覆盖行为记录）、D（升级）。
