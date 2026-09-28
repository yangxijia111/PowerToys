# 代码签名方案（CODE_SIGNING）

> 状态：**Binary Preview 阶段 fork 无代码签名**。本文档记录 Preview 期的降级策略、
> 威胁模型，以及未来引入签名时必须覆盖的资产清单与统一配置入口。
> 关联：[IDENTITY_AUDIT.md](IDENTITY_AUDIT.md) §4、[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)。

## 1. Preview 期降级策略（当前状态）

| 领域 | 降级措施 | 状态 |
|---|---|---|
| Win11 新式右键菜单（sparse MSIX ×5） | 注册失败被捕获并记日志（`package.h` RegisterSparsePackage 返回 false），**不阻断安装与模块启用**；Settings「关于」卡片与 README 明确标注 `Unsigned Preview limitation` | 已知限制 |
| 经典右键菜单（HKCU ContextMenuHandlers） | 不依赖签名，正常可用（已在真机安装验证） | 可用 |
| 自动更新 | **整体禁用**（`UpdateUtils.cpp` `FORK_AUTO_UPDATE_INSTALL_ENABLED=false`）：后台 worker 不启动；所有"检查/立即更新"入口改为打开 GitHub Releases 页，用户手动下载 + 自行核对 SHA256 | 已禁用 |
| pipe 客户端鉴权 | 关闭微软签名项（`FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE=0`），保留 目录+basename+版本 三重 fail-closed 校验 | 降级运行 |
| 安装器 | MSI/EXE 未签名；Windows SmartScreen 可能提示"未知发布者"，README 已说明 | 已知限制 |

**不做的事**：不伪造、不禁用、不绕过 Windows 的签名校验机制（WinVerifyTrust、MSIX 签名验证保持原样）。
sparse MSIX 注册失败是 Windows 安全机制的**正确行为**，fork 选择接受降级而不是绕过。

## 2. Preview 期威胁模型（IPC 残余风险，如实记录）

Runner 管道服务端的安全边界 = 显式 DACL（server SID + SYSTEM 完全控制；logon SID 仅数据读写权限）
+ `FILE_FLAG_FIRST_PIPE_INSTANCE` 防抢占 + `PIPE_REJECT_REMOTE_CLIENTS` + 客户端
目录/basename/版本 三重校验（fail-closed，每 PID 缓存）。

**残余缺口**：与上游官方 Release 相比，唯一差异是签名校验。同用户、同登录会话的进程
（与 runner 同等权限）理论上可以替换安装目录内的 `PowerToys.Settings.exe` 后通过鉴权——
该缺口上游用「微软签名校验」闭合；fork 无签名无法闭合。
但注意：能写入 `%LOCALAPPDATA%\PowerToysCuin` 的同用户攻击者已经可以替换任何将被用户
下次启动的 EXE，IPC 门禁不是额外攻击面。**评估结论：Preview 阶段安全水平可接受，
不构成 Binary Preview blocker；正式签名后恢复签名校验即可闭合。**

## 3. 未来签名必须覆盖的资产

### 3.1 EXE / DLL（全部发行二进制）
- `PowerToys.exe`（runner）、`PowerToys.ActionRunner.exe`、`PowerToys.Update.exe`
- `WinUI3Apps\PowerToys.Settings.exe`、`PowerToys.QuickAccess.exe`、`PowerToys.AdvancedPaste.exe` 等 WinUI3Apps 全部 EXE
- 各模块 DLL/EXE（`PowerToys.*.dll` / 模块 UI exe，含 FileLocksmithExt、PowerRenameExt、ImageResizerExt、NewPlus shell 扩展 DLL、`PowerToys.FileExplorerDLLExporter.dll`）
- `Tools\*.exe`（BugReportTool 等）、`bin\*.exe`（CLI shim）

### 3.2 安装器
- `PowerToysCuin-<ver>-x64.exe`（Bootstrapper，必须签名——SmartScreen 信誉的根）
- `PowerToysCuin-<ver>-x64.msi`（MSI，双签名： Authenticode + MSI 数据库签名）
- `PowerToysSetupCustomActionsVNext.dll`（CA DLL）

### 3.3 MSIX（需要证书 + 身份联动）
- `PowerToysSparse.msix`（PackageIdentity）
- 4 个上下文菜单包（`PowerRenameContextMenu` / `ImageResizerContextMenu` / `FileLocksmithContextMenu` / `NewPlusContextMenu`）
- `Microsoft.CmdPal.UI_*.msix`
- **联动项**：MSIX Publisher 变更会连带 ① manifest Publisher、② publisher hash（family 后缀）、
  ③ 安装器 CA 硬编码 family（`CustomAction.cpp`）、④ `installer.cpp` 的 MSIX 卸载锚 —— 四处必须一次性改齐。

### 3.4 信任链切换点（签名就绪后的 checklist）
1. `UpdateUtils.cpp`：`FORK_AUTO_UPDATE_INSTALL_ENABLED = true`。
2. `updating/installer.cpp`：`MICROSOFT_ORGANIZATION_NAME` / `verify_installer_trust` 签名者校验改为 fork 签名者（O=）；UpgradeCode 白名单已是 fork 值（MsiUtils.h）。
3. `FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE=1`（pipe 客户端签名校验恢复）。
4. sparse MSIX 身份改 fork Publisher + 四处 family hash 联动（IDENTITY_AUDIT §4-2）。
5. `Branding.cs` / About / README 的 "Unsigned Preview" 标注移除。

## 4. 统一配置入口

- **`ForkSigning.props`**（仓库根）：`ForkCodeSignEnabled` / 证书指纹 / 时间戳服务 / signtool 参数。
  `Directory.Build.props` 条件导入；`ForkCodeSignEnabled=true` 时构建后对 §3.1/3.2 资产跑 signtool。
- **pipe 宏**：`FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE`（`settings_window.cpp` / `quick_access_host.cpp`）。
- **更新开关**：`FORK_AUTO_UPDATE_INSTALL_ENABLED`（`UpdateUtils.cpp`）。
- 仓库内**不得**提交证书、私钥、密码或 Token；CI 仅使用最小权限 `GITHUB_TOKEN` 与 secrets 注入。
