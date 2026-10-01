# Cuin 自定义模块开发指南（CUSTOM_MODULE_GUIDE）

> 适用对象：PowerToys Cuin (Community Edition) fork，基于上游 `microsoft/PowerToys` 0.101+。
> 目标：为新增一个 **Cuin 自有功能模块** 提供可复用的完整 checklist。
> 原则：**新增文件 > 修改上游文件**；必须修改上游文件时一律加 `[fork-feature]` 注释锚点，并同步更新 `docs/SYNC_GUIDE.md`。

---

## 1. PowerToys 模块架构总览

上游共有 **三种模块形态**，Cuin 自有功能一律采用第 3 种（独立 UI 进程型）：

| 形态 | 代表模块 | 进程模型 | 适用场景 |
|---|---|---|---|
| A. C++ in-proc DLL | FancyZones、MouseUtils 系列 | runner 进程内加载 DLL，无独立 UI | 纯 hook / 无 XAML UI |
| B. C# 库 + shell 扩展宿主 | FileLocksmith、RegistryPreview、NewPlus | UI 在 Explorer 宿主或 Settings 内 | shell 集成 |
| **C. C++ 壳 + C# WinUI3 独立进程** | **Peek、MeasureTool、CmdPal** | runner 加载 C++ 壳 DLL，壳负责启停一个 WinUI3 unpackaged exe | **任何带自有窗口 UI 的功能** |

### 1.1 形态 C 的组成（以 Peek 为样板）

```
src/modules/peek/
├── peek/                    # C++ 壳（ModuleInterface DLL，被 runner 直接加载）
│   ├── dllmain.cpp          # 模块类：enable/disable/is_enabled/on_hotkey/get_hotkeys/init_settings
│   ├── peek.vcxproj         # TargetName=PowerToys.Peek，输出 WinUI3Apps\
│   ├── trace.cpp/h、pch、packages.config、.rc
├── Peek.UI/                 # C# WinUI3 unpackaged 主程序（常驻进程）
│   ├── Peek.UI.csproj       # WindowsPackageType=None + WindowsAppSDKSelfContained + SelfContained
│   ├── PeekXAML/App.xaml.cs # 入口：Logger → DI Host → NativeEventWaiter 等 ShowEvent
│   └── NativeEventWaiter.cs # 命名事件等待器
├── Peek.Common/             # 纯 C# 业务库（可单测，无 UI 依赖）
└── Peek.Common.UnitTests/   # 纯 .NET 单测
```

### 1.2 生命周期（runner → 模块）

runner（`src/runner/main.cpp:257` `knownModules` 列表）对每个模块 DLL：

1. `LoadLibrary` + 调 `powertoy_create()`（工厂函数，`src/modules/interface/powertoy_module_interface.h:191`）；
2. 调 `get_key()` 拿非本地化模块名 → 存入 `modules()` map；
3. 读 General settings 的 `enabled` JSON 对象（`src/runner/general_settings.cpp`，key 必须等于 `get_key()` 返回值），为 true 则调 `enable()`；
4. 用户在 Settings 切开关 → runner 调 `disable()`/`enable()`；
5. `disable()` 应释放进程与资源（壳里 SetEvent 终止事件 → 等 UI 进程退出 → 超时强杀）；
6. 退出时 `destroy()` + 卸载 DLL。

**接口**：`PowertoyModuleIface`（`src/modules/interface/powertoy_module_interface.h:37`）。最小实现只需 `get_name/get_key/get_config/set_config/enable/disable/is_enabled/destroy`；需要全局热键时再实现 `get_hotkeys()/on_hotkey()`。

官方还提供空壳模板 `tools/project_template/ModuleTemplate/`（README + vcxproj + dllmain），可作 C++ 壳起点。

### 1.3 热键链路（形态 C 标准姿势）

1. 壳 `init_settings()` 从模块 settings.json 读 `properties.activation_shortcut`（JSON: `win/ctrl/shift/alt/code`）；
2. `get_hotkeys()` 把热键交给 runner → `src/runner/powertoy_module.cpp:56` `update_hotkeys()` 注册进 `CentralizedKeyboardHook`（集中式低键位钩子，与 KBM 统一、自带冲突检测）；
3. 按键 → 壳 `on_hotkey()` → UI 进程未运行则先 `launch_process()` → `SetEvent(m_hInvokeEvent)`；
4. C# 侧 `NativeEventWaiter.WaitForEventLoop(<事件名>, callback)` 收到事件 → 弹出窗口。

**不要**在 C# exe 里自用 `RegisterHotKey`：绕过集中热键管理会失去冲突检测和 Settings 可配置性。

### 1.4 IPC（形态 C 标准姿势）

- **runner → UI 进程**：命名 Win32 事件（`CreateEventW`/`EventWaitHandle`），常量定义在 `src/common/interop/shared_constants.h`（如 `SHOW_PEEK_SHARED_EVENT = L"Local\\ShowPeekEvent"`）。
- 需要在 C# 侧拿到事件名时，经 WinRT 投影：`src/common/interop/Constants.idl` + `Constants.cpp` 各加一个静态方法。
- **UI 进程存活性**：壳启动 exe 时把 **runner PID 作为唯一命令行参数** 传入；C# 侧 `RunnerHelper.WaitForPowerToysRunner(pid, ...)` 监视 runner 退出即自杀（防孤儿进程）。
- 需要双向 JSON 消息的复杂模块才用命名管道（如 PowerLauncher）；简单面板不需要。

### 1.5 Enabled 状态

- 持久化位置：`%LOCALAPPDATA%\PowerToysCuin\settings.json`（**General settings**）的 `enabled` 对象内，**key = 模块名**。
- C# 侧对应 `src/settings-ui/Settings.UI.Library/EnabledModules.cs` 里的 bool 属性（`[JsonPropertyName("模块名")]`）。
- 关闭时 runner 调 `disable()`，壳必须让 UI 进程退出（禁用态零进程、零 CPU）。

### 1.6 模块自身 settings 持久化

- 路径：`%LOCALAPPDATA%\PowerToysCuin\<ModuleName>\settings.json`（fork 数据根 `PowerToysCuin` 唯一来源：C++ `shared_constants.h` `APPDATA_PATH`、C# `ManagedCommon/Branding.cs` `ForkAppDataFolderName`）。
- C# 类型实现 `ISettingsConfig`（`ToJsonString()/GetModuleName()/UpgradeSettingsConfiguration()`），属性体拆 `Properties` 类，均需注册进 AOT 序列化上下文（见 §2.D）。
- **损坏 JSON 必须安全回退默认值**：`SettingsUtils.GetSettingsOrDefault<T>` 已兜底，模块自己不要直接 `File.ReadAllText + Deserialize`。
- 版本字段用基类 `BasePTModuleSettings` 的 `version`，升级逻辑写在 `UpgradeSettingsConfiguration()`。

### 1.7 日志 / 遥测 / 本地化

- **日志**：C# 侧 `ManagedCommon.Logger.InitializeLogger("\\<ModuleName>\\Logs")` → `%LOCALAPPDATA%\PowerToysCuin\<ModuleName>\Logs\<版本>\Log_yyyy-MM-dd.log`。C++ 壳用 spdlog trace（抄 Peek `trace.cpp`）。**Cuin 模块不接任何远程遥测**，只有本地日志。
- **本地化**：模块 UI 资源放 `<ModuleName>.UI` 项目内自己的 resw（`Strings/en-us/` + `Strings/zh-CN/`）；Settings 侧字符串加进 Settings 的两个 resw（§2.D）。**禁止硬编码用户可见字符串**。仓库门禁 `python tools/check_zh_cn_coverage.py --fail-on-orphan` 会检查孤儿键。
- **提权**：默认 asInvoker。需要管理员的一次性动作走按需提权（`runas` 动词 / `CoCreateInstance(Elevation)`），**不得**让整个 runner 或模块进程常驻提权。Peek 的参考实现：runner 提权时壳用 `RunNonElevatedFailsafe` 强制 UI 进程降权运行。

---

## 2. 新增 Cuin 模块完整 Checklist

约定：模块接口名（= `get_key()` = settings 目录名 = JSON key = `ModuleType` 枚举名）**建议带 `Cuin` 前缀**，避免与上游未来模块撞名。下文以 `<X>` 代称。

### A. 模块本体（全部全新，放 `src/modules/cuin/` 下）

| 文件 | 说明 |
|---|---|
| `src/modules/cuin/<x>/<x>.vccxproj` + `dllmain.cpp` + `trace.*` + `pch.*` + `packages.config` + `.rc` | C++ 壳，抄 Peek（`src/modules/peek/peek/`）或官方 `tools/project_template/ModuleTemplate/`。TargetName=`PowerToys.<X>`，输出 `WinUI3Apps\` |
| `src/modules/cuin/<X>.UI/` | WinUI3 主程序，抄 `Peek.UI.csproj` 关键属性：`WindowsPackageType=None`、`WindowsAppSDKSelfContained=true`、import `Common.SelfContained.props`、`OutputPath=$(RepoRoot)$(Platform)$(Configuration)\WinUI3Apps`、app.manifest asInvoker |
| `src/modules/cuin/<X>.UI/Strings/{en-us,zh-CN}/Resources.resw` | 模块自身 UI 字符串 |
| `src/modules/cuin/<X>.Common/`（可选） | 纯 C# 业务逻辑库（与 Windows API 分层，保可测性） |
| `src/modules/cuin/<X>.UnitTests/` | 单元测试（见 §3） |

### B. runner（修改 1 处上游文件）

- `src/runner/main.cpp` `knownModules` 数组**尾部追加**一行 `L"WinUI3Apps/PowerToys.<X>.dll",`，行尾注释 `// [fork-feature] Cuin custom module: <X>`。

### C. IPC 事件常量（仅当需要新命名事件）

- `src/common/interop/shared_constants.h`：加 `SHOW_<X>_SHARED_EVENT` / `TERMINATE_<X>_SHARED_EVENT`（`[fork-feature]` 锚点）。
- `src/common/interop/Constants.idl` + `Constants.cpp`：如 C# 需要投影事件名，加静态方法（`[fork-feature]` 锚点）。

### D. Settings 集成（新建为主，修改集中在 5 个上游文件）

**新建（不产生 merge 冲突）：**
1. `src/settings-ui/Settings.UI.Library/<X>Settings.cs` — `ModuleName` 常量 + `ISettingsConfig`（抄 `PeekSettings.cs`）
2. `src/settings-ui/Settings.UI.Library/<X>Properties.cs` — 属性体（抄 `PeekProperties.cs`；热键字段名用 `activation_shortcut` 可复用现成热键控件）
3. `src/settings-ui/Settings.UI/ViewModels/<X>ViewModel.cs`（抄 `PeekViewModel.cs`）
4. `src/settings-ui/Settings.UI/SettingsXAML/Views/<X>Page.xaml(.cs)`（抄 `PeekPage.xaml`；Page 由 csproj SDK 自动 glob，无需改 csproj；搜索索引由 XamlIndexBuilder 构建期自动生成）
5. `Settings.UI\Assets\Settings\Icons\<X>.png` + `Assets\Settings\Modules\<X>.png`（导航图标 + 页头图）

**修改上游文件（全部加 `[fork-feature]` 锚点）：**
6. `src/common/ManagedCommon/ModuleType.cs` — 枚举加 `CuinX`（Dashboard 会自动遍历枚举生成开关列表）
7. `src/settings-ui/Settings.UI.Library/EnabledModules.cs` — 加 bool 属性 `[JsonPropertyName("<X>")]`（默认值即模块默认状态，需明确写注释）
8. `src/settings-ui/Settings.UI.Library/Helpers/ModuleHelper.cs` — `GetIsModuleEnabled` / `SetIsModuleEnabled` / `GetModuleKey` 三处 case
9. `src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs` — `[JsonSerializable]` ×2（Settings + Properties；**AOT 必需，漏了运行时抛异常**）
10. `src/settings-ui/Settings.UI/SerializationContext/SourceGenerationContextContext.cs` — IPC 序列化注册
11. `src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml` — NavigationViewItem，挂进合适分组（fork 已重组为 SystemTools / WindowingAndLayouts / InputOutput / ScreenTools / FileManagement / Advanced）
12. `src/settings-ui/Settings.UI/App.xaml.cs` — `GetPage` switch 加 `case "<X>": return typeof(<X>Page);`
13. `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` + `zh-CN/Resources.resw` — `Shell_<X>.Content`、`<X>.ModuleTitle`、`<X>.ModuleDescription` + 页内控件 keys（en-us 只做文本级插入，勿整文件重写）
14. `src/settings-ui/Settings.UI.Helpers/ModuleGpoHelper.cs`（在 Settings.UI 项目内）— GPO case（仅当需要 GPO 管控；Cuin 模块可不加，见 §4）

**按需：**
- OOBE 四件套：`OOBE/Enums/PowerToysModules.cs`、`OOBE/ViewModel/OobeShellViewModel.cs`、`OobeWindow.xaml`、`OobeWindow.xaml.cs` + 新建 `OOBE/Views/Oobe<X>.xaml`（第一版可不进 OOBE）
- `Settings.UI/Services/Presets/PresetsCatalog.cs` — 内置预设（可选）
- 测试：`Settings.UI.UnitTests/ViewModelTests/<X>.cs` + `ModelsTests/<X>SettingsTests.cs`（mock 用现成泛型 `ISettingsUtilsMocks.GetStubSettingsUtils<T>`，无 per-module mock 基建）

### E. 解决方案注册（slnx 不 glob，必须手工）

`PowerToys.slnx` 加 `<Folder Name="/modules/Cuin/">` + 各 `<Project Path=...>` 元素（参照 `/modules/Peek/` 段）。注意 `PowerToys.Settings.slnf` 是过滤器文件，不含 modules 项目，不用动。

### F. Installer

- **主 payload 零改动**：exe/dll/pri 只要输出到 `WinUI3Apps\` 就被 `installer/PowerToysSetupVNext/generateAllFileComponents.ps1`（glob `**/*` 列表）自动收进 MSI。
- **仅当模块有 `WinUI3Apps\Assets\<X>\` 子目录**时：新建 `<X>.wxs`（抄 `Peek.wxs`）、`Product.wxs` CoreFeature 加 `<ComponentGroupRef>`、ps1 加 `Generate-FileList` 两行、`PowerToysSetupCustomActionsVNext.vcxproj` PreBuildEvent 加 `.bk` 备份行、`PowerToysInstallerVNext.wixproj` PostBuildEvent 加还原行（**修改的 4 个文件全加 `[fork-feature]` 锚点**）。

### G. CI（不建新 workflow）

- 快速 CI `.github/workflows/cuin-ci.yml`：只构建 runner + Settings.UI.UnitTests + C++ CommonUtils。**新模块的 csproj 构建与单测要自行加 step**（msbuild `<X>.UnitTests.csproj` + vstest 过滤），照现有 Settings.UI.UnitTests 步骤抄。
- Release CI `cuin-release.yml`：全解决方案构建，进 slnx 即自动编进；测试 step 若要跑新单测同样加行。
- push 后必须等 `Cuin CI` + `Spell checking` 两个 check 真实跑绿（新词进 `.github/actions/spelling/expect.txt` fork 段，**禁 CamelCase 复合词条目**）。

### H. 数据与安全红线（每个 Cuin 模块必须遵守）

- 数据只写 `%LOCALAPPDATA%\PowerToysCuin\`，禁止写 `Microsoft\PowerToys`；
- 不新增远程遥测 / 网络请求 / 后台轮询 / 高频 timer；
- 禁止任意命令执行输入框、shell 字符串拼接、未验证路径加载；外部进程调用必须**固定 executable + 结构化/固定参数**；
- 危险操作（杀进程 / 重启 Explorer / 注销 / 关机）必须有明确确认对话框，UI 上视觉区分；
- 禁用态零进程零 CPU；空闲态事件驱动；
- settings schema 带 `version`，损坏 JSON 回退 safe defaults，绝不让 Runner 崩溃。

---

## 3. 测试要求

| 层 | 项目 | 必测 |
|---|---|---|
| 业务逻辑 | `<X>.UnitTests`（纯 .NET） | 动作目录/映射完整性、settings load/save/invalid fallback/向后兼容、危险动作确认标记、执行器调用参数（mock 捕获）、重复调用幂等 |
| Settings 集成 | `Settings.UI.UnitTests` | ViewModel 开关行为、enabled 持久化（抄现有 ViewModelTests 模式） |

分层原则：业务逻辑与 Windows API 调用分离（`I<Action>Executor` 接口 + 实现），单测只测逻辑层，`mock` 掉 Shell/进程/剪贴板。**测试绝不真实执行** shutdown/logout/杀系统进程/删文件。

---

## 4. Fork 边界与上游同步

1. **目录集中**：Cuin 自有代码只放 `src/modules/cuin/`（模块本体）+ `src/settings-ui/` 下按上游结构不得不动的注册点。
2. **锚点**：所有上游文件修改处用 `// [fork-feature] <一句话>` 或 XAML 注释 `<!-- [fork-feature] ... -->`。GPO 若不接（Cuin 模块默认不接），`ModuleGpoHelper` 不动，避免多一处冲突热点。
3. **每次新增模块后更新 `docs/SYNC_GUIDE.md`** 的冲突热点表：目前热点排序为 `ShellPage.xaml` > `en-us Resources.resw` > `[fork-identity]` 身份文件 > `App.xaml.cs` > `main.cpp knownModules`（尾部追加型，冲突概率低）。
4. **merge upstream 前自查**：`git diff upstream/main --stat` 中除锚点行外不应出现 Cuin 专属逻辑散落。
5. **已知 fork 隐患**：`Settings.UI.Library/Utilities/Helper.cs` `GetFileWatcher` 仍硬编码 `Microsoft\PowerToys` 路径（上游遗留，导致模块 settings 热更新监视失效——读路径正确所以功能不受损）。Cuin 模块 C# 侧热更新暂依赖进程重启或轮询规避，待统一修复。

---

## 5. 常见坑（实战积累）

- **AOT 序列化**：Settings/UI 两侧序列化上下文都要注册新类型，漏一处 = 运行时 `InvalidOperationException`。
- **slnx 不 glob**：新 csproj 忘记加 `PowerToys.slnx` → 本地单项目构建正常、CI 全量构建缺失。
- **resw**：en-us 资源只做键级插入；zh-CN 全量翻译时注意占位符 `{0}`/`%s`、`\r\n`、XML 转义必须与 en-us 完全一致（`i18n_tools` 有校验脚本）。
- **installer 三连构建**：每次构建前要重新 prep（`.bk` 恢复机制），PostBuild 会消耗占位 wxs。
- **本地构建**：`_CL_=/MP2`（XAML 大文件 /MP4 会 OOM）、`/m:2`、构建前看 `FreePhysicalMemory` 和 D 盘余量（全量产物 ~90GB）。
- **vstest 过滤器**用 `FullyQualifiedName~`，`Name~` 在本机版本不匹配 FQN。
- **新增 C++ 项目**记得 import 该有的 props（如 `deps/spdlog.props`），否则 PCH 宏缺失 C2220（上游 AutoHideCursor 踩过）。
- **CI 拼写**：新单词（含模块名）加 `.github/actions/spelling/expect.txt` fork 段；expect.txt 禁止 CamelCase 复合词条目（拆成单词）。
