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

## 5. 两种运行模式（Phase 6 起，正式架构）

| 模式 | 触发条件 | 行为 | 伪装防护 |
|---|---|---|---|
| **Unsigned Preview**（默认） | CI 未配置签名 secrets；本地 `ForkCodeSignEnabled=false` | 与 preview.1 完全一致的 unsigned 构建；签名步骤自动跳过，CI 不失败 | 从不声明"已签名"；SmartScreen/README 提示保持 |
| **Signed Release** | CI secrets 注入证书（见 §5.2）；本地传 `-CertThumbprint` | payload 布局 → CA DLL → 三件套全链路 signtool 签名（SHA256 + RFC3161 时间戳），签名后强制 `-Verify` gate | 构建侧 fail-loud：声明启用但缺凭证直接 Error；脚本签后复核 |

### 5.1 职责划分

- **构建期（MSBuild / `ForkSigning.props`）**：只做配置声明、宏传播（pipe 签名校验开关）与
  fail-loud 校验（`ForkSignValidateConfig` Target）。**不执行签名**——超大仓库逐项目签名既慢又
  易漏，与上游 ESRP pipeline 签名模式保持一致（上游也不在 msbuild 里签）。
- **打包期（`tools/fork_sign_assets.ps1`）**：实际签名。
  - `-Stage Payload -Root x64\Release`：安装后落盘的全部 EXE/DLL/MSIX（§3.1/3.3），
    **必须在 MSI 构建前执行**（结果被 heat/WiX 打进安装包）；CA DLL 输出目录单独调用一次。
  - `-Stage Installers -Root <UserSetup/MachineSetup>`：三件套（§3.2），必须在
    Collect artifacts + SHA256 之前执行，保证 `SHA256SUMS.txt` 反映签名后文件。
  - `-Verify`：只校验签名状态（`Get-AuthenticodeSignature`），未通过即非零退出，可作 CI gate。
  - 凭证：`-CertThumbprint`（本机证书存储，开发者场景）或 `-PfxPath + -PfxPassword`（CI 场景）；
    两者都缺失时**直接失败**，绝不静默跳过。

### 5.2 CI 接入（cuin-release.yml，已就位）

- Secret 名称（**仅示例**，按需在仓库 Settings → Secrets 配置）：
  - `FORK_CODE_SIGN_PFX_BASE64`：PFX 文件的 Base64 编码
  - `FORK_CODE_SIGN_PFX_PASSWORD`：PFX 密码
- 未配置 secrets 时两个签名步骤经 `if: env.FORK_CODE_SIGN_PFX_BASE64 != ''` 自动跳过，
  workflow 其余部分零改动；配置后同一 workflow 无需改代码即可产出 Signed Release。
- PFX 在 runner 临时目录解码，签名结束 `finally` 立即删除；密码仅经环境变量传递，不进命令行与日志。

### 5.3 sparse MSIX 恢复（签名的直接收益）

安装器对 sparse MSIX 的注册是"无条件尝试 + 失败捕获降级"（`src/common/utils/package.h`
`RegisterSparsePackage` 返回 false 仅记日志；fork 未改动该路径）。因此：
- MSIX 签名并受信任后**无需代码变更**，Win11 新式右键菜单注册自动恢复；
- unsigned 时继续安全降级（Windows 信任机制原样保留，未绕过）；
- 防回归检查：`tools/check_sparse_registration.py` 静态断言注册调用链未被 fork 条件化禁用。

## 6. 未来启用签名的完整 checklist（不需要再改架构）

1. 采购证书（OV/EV code signing），**不提交任何私钥材料进仓库**；
2. 仓库配置 §5.2 两个 secrets；
3. 按 §3.4 切换信任链五项（自动更新、updater 签名者、pipe 校验、MSIX Publisher 四处联动、文案）；
4. 走 RELEASE_CHECKLIST 全流程构建，确认签名步骤执行 + Verify gate 通过；
5. 真机验证：SmartScreen 信誉、Win11 新式菜单注册、`Get-AuthenticodeSignature` 全部 Valid。
