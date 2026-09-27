# Fork 身份映射表（IDENTITY_MAP）

> 每一处**已修改**的发行身份：官方原值 → fork 新值 → 用途 → 文件位置。
> 修改任何身份项时必须同步更新本表与 `tools/check_fork_identity.py`。
> 保持官方值不改的项及理由见 [IDENTITY_AUDIT.md](IDENTITY_AUDIT.md) §3/§4。

## 1. 安装器身份

| 用途 | 官方值 | Fork 值 | 文件 |
|---|---|---|---|
| MSI UpgradeCode（perMachine） | `42B84BF7-5FBF-473B-9C8B-049DC16F7708` | `78975C14-0AA0-41A7-99C2-55F44200A919` | `installer/PowerToysSetupVNext/Common.wxi`（`UpgradeCodeGUID`，perMachine 分支） |
| MSI UpgradeCode（perUser） | `D8B559DB-4C98-487A-A33F-50A8EEE42726` | `DA33EB25-63A5-4E9A-8B04-D8AE81BB888D` | `installer/PowerToysSetupVNext/Common.wxi`（`UpgradeCodeGUID`，perUser 分支） |
| Bundle UpgradeCode | `6341382d-c0a9-4238-9188-be9607e3fab2` | `0A739D71-3045-42E6-8505-9DB32432F8E6` | `installer/PowerToysSetupVNext/PowerToys.wxs:1` |
| MSI Name | `PowerToys (Preview)` | `PowerToys Cuin (Community Edition)` | `installer/PowerToysSetupVNext/Product.wxs`（Phase 2 已改） |
| MSI/Bundle Manufacturer | `Microsoft Corporation` | `PowerToys Cuin Community` | `Product.wxs`（Package）、`PowerToys.wxs`（Bundle） |
| Bundle Name | `PowerToys (Preview) <arch>` | `PowerToys Cuin (Community Edition) <arch>` | `PowerToys.wxs`（Bundle） |
| 安装目录名 | `PowerToys` | `PowerToysCuin` | `Product.wxs`（`INSTALLFOLDER`、`DEFAULTBOOTSTRAPPERINSTALLFOLDER`）、`PowerToys.wxs`（`InstallFolder` ×2 scope） |
| 快捷方式名（开始菜单+桌面） | `PowerToys (Preview)` | `PowerToys Cuin` | `installer/PowerToysSetupVNext/Core.wxs` |
| 官方检测锚（**只用于阻止**，非 fork 身份） | 官方两个 UpgradeCode | 保留原值，`SearchInstalledOfficialPowerToysVersion/UserVersion` + 阻止条件 | `PowerToys.wxs`（bal:Condition）、`Product.wxs`（Upgrade OnlyDetect + Launch Condition） |

## 2. 运行时身份常量

| 用途 | 官方值 | Fork 值 | 文件 |
|---|---|---|---|
| AppData 设置/日志根 | `Microsoft\PowerToys` | `PowerToysCuin` | `src/common/interop/shared_constants.h`（`APPDATA_PATH`，C++/C# 共用唯一来源）；`src/common/SettingsAPI/settings_helpers.cpp`（`get_root_save_folder_location` / `get_local_low_folder_location` 引用之） |
| 托管侧 AppData 拼接 | `Microsoft\PowerToys` | `PowerToysCuin`（引用 `Microsoft.PowerToys.ManagedCommon.Branding.ForkAppDataFolderName`） | `src/settings-ui/Settings.UI.Library/SettingPath.cs`（4 处收敛为常量）、`src/settings-ui/Settings.UI.Library/Utilities/Helper.cs`（新增 `PowerToysAppDataFolder`）、`src/common/ManagedCommon/Branding.cs`（新增 `ForkAppDataFolderName`） |
| UI 测试辅助设置根 | `%LOCALAPPDATA%\Microsoft\PowerToys` | `%LOCALAPPDATA%\PowerToysCuin` | `src/common/UITestAutomation.Next/SettingsConfigHelper.cs`（`PowerToysSettingsRoot`） |
| MSI 身份常量（路径探测/更新白名单） | `{42B84BF7-...}`、`{D8B559DB-...}`、组件 `{A2C66D91-...}` | `{78975C14-...}`、`{DA33EB25-...}`、组件 `{30261594-41A6-4509-AD09-FBC4E692F441}` | `src/common/utils/MsiUtils.h`、`installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp` |
| 更新端点 | `repos/microsoft/PowerToys/releases/latest`、`.../releases?per_page=100` | `repos/yangxijia111/PowerToys/...`（同路径） | `src/common/updating/updating.cpp` |
| 更新引导器产品名锚 | `PowerToys (Preview)` | `PowerToys Cuin (Community Edition)` | `src/common/updating/installer.cpp`（`POWERTOYS_PRODUCT_NAME_PREFIX`） |
| pipe 客户端签名要求 | `requireMicrosoftSignature = true` | `false`（目录+basename+版本校验保留） | `src/runner/settings_window.cpp`、`src/runner/quick_access_host.cpp` |
| DSC 卸载项检测名 | `PowerToys (Preview)` | `PowerToys Cuin (Community Edition)` | `src/dsc/PowerToys.Settings.DSC.Schema.Generator/DSCGeneration.cs` |

## 3. 明确保留官方值的身份（节选，完整分析见 AUDIT §3/§4）

Runner mutex `Local\PowerToys_Runner_MSI_InstanceMutex`（安全带）、托盘窗口类 `PToyTrayIconWindow`、
全部 `Local\PowerToys*` 事件、全部 COM CLSID（菜单/预览/Toast）、AUMID `Microsoft.PowerToysWin32`、
`powertoys://` 协议、计划任务 `\PowerToys`、服务 `PowerToys.MWB.Service`、
sparse MSIX ×5（`Microsoft.PowerToys.SparseApp` + 4 个 ContextMenu 包）、
注册表 bookkeeping（`Software\Classes\powertoys`、`Software\Microsoft\PowerToys`）、GPO 键。

## 4. 维护规则

1. 新增/修改身份项：改代码 → 更新本表 → 更新 `tools/check_fork_identity.py` 断言 → 打 `[fork-identity]` 注释锚点。
2. 生成新 GUID 必须记录用途与本表行号，禁止复用表内 GUID 于其他用途。
3. 上游 merge 时：凡带 `[fork-identity]` 锚点的 hunk 一律保留 fork 侧；官方 UpgradeCode 若在
   上游新代码中作为**检测锚**出现是正常的，作为 **fork 自身身份**出现则说明合并出错了
   （跑 `python tools/check_fork_identity.py` 即可发现）。
