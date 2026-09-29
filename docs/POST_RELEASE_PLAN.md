# Post-Release Stabilization Plan（发布后稳定化计划）

> 阶段：Phase 6 / Post-release Stabilization
> 基准版本：`v0.1.0-preview.1`（Pre-release，2026-09-29 发布，tag `0fe746ea4`）
> 分支：`cuin-dev`（`main` 保持不动）
> 关联：[CODE_SIGNING.md](CODE_SIGNING.md)、[RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)、[IDENTITY_AUDIT.md](IDENTITY_AUDIT.md)

## 1. preview.1 冻结原则

- tag `v0.1.0-preview.1` 与已发布资产（EXE / perUser MSI / perMachine MSI / `SHA256SUMS.txt`）**不移动、不替换、不重传**。
- 任何修复统一进入未来的 `v0.1.0-preview.2`；本阶段**不创建** preview.2 的 tag / release。
- `main` 分支保持不动，所有维护工作发生在 `cuin-dev`。

## 2. preview.1 已知限制（发布时已声明）

| # | 限制 | 原因 | 影响等级 | 计划 |
|---|---|---|---|---|
| K1 | 安装包未签名，SmartScreen 可能提示"未知发布者" | 无代码签名证书 | P2（体验） | 签名启用后消除（§5） |
| K2 | Win11 新式右键菜单（PowerRename / Image Resizer / File Locksmith / New+）不可用 | sparse MSIX 未签名，Windows 拒绝注册（正确行为） | P2（功能降级） | 签名后自动恢复（§5.3） |
| K3 | 自动更新禁用，需手动下载更新 | 未签名安装包无法通过更新器信任校验 | P2（体验） | 签名后启用（CODE_SIGNING §3.4） |
| K4 | 不支持与官方 PowerToys 共存（安装器主动阻止） | 双方 Shell 扩展 / 通知 / 设置互踩 | 设计决定（非缺陷） | 维持现状 |
| K5 | 卸载不删除用户配置（`%LOCALAPPDATA%\PowerToysCuin`） | 与官方行为一致 | 预期行为 | 维持现状 |

## 3. 发布后状态快照与问题分类（2026-09-29）

- GitHub Release `v0.1.0-preview.1`：Pre-release，4 资产公开可下载，README 下载入口就绪。
- Issues：**0 个**（尚无用户反馈）。
- CI：`Cuin CI` / `Spell checking` / `Cuin Release Build` 全绿；Store / WinGet 提交 workflow 按预期跳过。
- 已知测试豁免：3 个需 elevation 的 C++ pipe 测试在非提权 runner 上按环境限制标注，不伪装通过。

分类口径（本阶段只主动修 P0/P1 与明确低风险 P2）：

| 级别 | 定义 | 当前条目 |
|---|---|---|
| P0 | 安装失败、数据损坏、安全问题、无法启动 | 无 |
| P1 | 核心功能明显不可用、卸载/升级问题 | 无（无用户报告；升级链静态验证见 §6） |
| P2 | UI、翻译、兼容性、小功能 | K1/K2/K3（均由签名缺位派生，无独立修复项） |
| P3 | 建议与增强 | 待用户反馈积累 |

> 结论：**preview.1 无 P0/P1，不触发 preview.2**（准入标准见 §7）。

## 4. 稳定化工作项（Phase 6 范围）

| 项 | 内容 | 状态 |
|---|---|---|
| S1 | check-spelling 降噪：fork 词汇分类入典 + `only_check_changed_files` 隔离上游遗留警告 | 完成（2026-09-29，commit `chore: adapt spelling checks for fork terminology`） |
| S2 | 签名就绪架构：`ForkSigning.props` fail-loud 校验 + `tools/fork_sign_assets.ps1` 打包期签名/验证脚本 + CI 条件步骤（Secret 仅示例名） | 完成（见 CODE_SIGNING.md §4-§6） |
| S3 | sparse MSIX 恢复确认：注册路径未被禁用，签名后自动恢复；静态检查防回归 | 完成（见 §5.3） |
| S4 | Bug Report 流程：Issue 模板 + README 反馈指引 + 日志脱敏提醒 | 完成 |
| S5 | preview.1 → preview.2 升级链静态验证（UpgradeCode / ProductCode / 数据保留）+ CI 集成 | 完成（见 §6） |
| S6 | CI / Release pipeline 复查（gates 保留、restore 无本机缓存依赖） | 完成 |

禁止事项（Phase 6 期间）：新大型 CustomModule、UI 全面重做、namespace/CLSID 全局改名、仓库重命名、品牌迁移、发布 stable、移动 preview.1 tag / 覆盖 preview.1 资产。

## 5. 签名计划（Unsigned Preview → Signed Release）

### 5.1 两种模式

| 模式 | 触发条件 | 行为 |
|---|---|---|
| **Unsigned Preview**（默认） | CI 未注入签名 secrets / 本地 `ForkCodeSignEnabled=false` | 与 preview.1 完全一致的 unsigned 构建；CI 不失败、不伪装已签名 |
| **Signed Release** | secrets 注入证书 + 显式开启 | 打包完成后对 §5.2 清单统一 signtool 签名；sparse MSIX 注册自动恢复；信任链切换按 CODE_SIGNING §3.4 checklist |

### 5.2 需要签名的资产（审计结论，详见 CODE_SIGNING.md §3）

1. **EXE/DLL**：runner（`PowerToys.exe`）、ActionRunner、Update、全部 WinUI3Apps EXE、模块 DLL/EXE、shell 扩展 DLL（PowerRenameExt / ImageResizerExt / FileLocksmithExt / NewPlus）、FileExplorerDLLExporter、Tools/BugReportTool、bin 下 CLI shim。
2. **安装器**：Bootstrapper EXE（SmartScreen 信誉根）、两个 MSI（Authenticode）、CA DLL。
3. **MSIX**：PowerToysSparse.msix + 4 个上下文菜单包 + CmdPal 包；Publisher 变更需四处 family 联动（manifest / publisher hash / CustomAction / installer.cpp 锚）。
4. **信任链**：updater（`verify_installer_trust`）、IPC pipe 客户端签名校验（`FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE`）、sparse 注册。

### 5.3 sparse MSIX 恢复机制（验证结论）

- 安装器对 `PowerToysSparse.msix` 的注册是**无条件尝试 + 失败捕获降级**（`src/common/utils/package.h` `RegisterSparsePackage` 返回 false 仅记日志，不阻断安装；fork 未改动该路径）。
- 因此：MSIX 签名并受信任后**无需代码变更**即恢复 Win11 新式菜单注册；unsigned 时继续安全降级（Windows 信任机制保持原样，未绕过）。
- 防回归：`tools/check_sparse_registration.py` 静态断言注册调用链未被 fork 条件化禁用、失败路径不阻断安装。

### 5.4 证书纪律

不购买证书、不生成/提交真实私钥、仓库内无 PFX/密码/Base64 私钥；CI Secret 名称仅示例（`FORK_CODE_SIGN_PFX_BASE64` / `FORK_CODE_SIGN_PFX_PASSWORD`）；未配置 secrets 时 CI 保持 unsigned 构建成功。

## 6. preview.1 → preview.2 升级链验证

设计要求：preview.2 MSI 可**覆盖升级** preview.1，不要求手动卸载。静态验证项（`tools/check_upgrade_chain.py`，已纳入 CI）：

- UpgradeCode 跨版本**不变**（MSI 识别同一产品族）且为 fork 专属值（不与官方冲突）；
- ProductCode 由 WiX `Product @Id="*"` 自动生成（每次构建变化，允许 major upgrade）；
- `REMOVE=ALL` 卸载语义不删 `%LOCALAPPDATA%\PowerToysCuin` 用户数据（安装器仅写安装目录 + HKCU/HKLM 配置键）；
- Settings / OOBE / Presets 状态位于用户数据目录，升级不触碰。

运行时（真机）验证推迟到 preview.2 发布前的发布检查（RELEASE_CHECKLIST §4 场景复验）。

## 7. preview.2 准入标准（任一满足才准备）

1. preview.1 出现真实 P0/P1 Bug；
2. Installer / upgrade 存在必须修复的问题；
3. 签名链正式启用；
4. Win11 context menu 获得明显修复（等价于签名启用）；
5. 多个明确的小修复值得集中发布。

> **当前结论（2026-09-29）：条件均不满足 → STABILIZATION COMPLETE — KEEP v0.1.0-preview.1。**

## 8. 下一阶段建议

1. 观察期：收集真实用户 Issue（当前 0），按 §3 口径分类；
2. 签名决策：若确认采购证书，按 CODE_SIGNING.md §5 启用 Signed Release 模式并触发 preview.2（同时恢复 sparse 菜单 + 自动更新）；
3. 小步维持：P2/P3 修复累计到值得集中发布时，按 RELEASE_CHECKLIST 走 preview.2 流程；
4. 上游同步：定期评估 SYNC_GUIDE 流程，避免落后上游安全修复。
