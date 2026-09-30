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

### 6.1 真机预演发现：同 ProductVersion 拒绝升级（2026-09-30，已修复）

真机升级预演前的 MSI 属性核对发现：preview.1 的 MSI `ProductVersion = 0.1.0`，而 Version 生成链不会把
preview 序号编入 ProductVersion——preview.2 与 preview.1 的 ProductVersion 完全相同。同版本 + 不同
ProductCode 时三层机制**全部**拒绝覆盖安装：

1. MSI `MajorUpgrade`（未设 `AllowSameVersionUpgrades`，同版本不调度 `RemoveExistingProducts`）→ 报 1638
   "Another version of this product is already installed"；
2. `Product.wxs` 手写 `UpgradeVersion Maximum="$(var.Version)" IncludeMaximum="no"`（同版本不满足
   `< Maximum`）→ `PREVIOUSVERSIONSINSTALLED` 不设置；
3. Bootstrapper `bal:Condition TargetPowerToysVersion >= DetectedForkPowerToysUserVersion`（0.1.0 >= 0.1.0
   为真）→ 引导器弹出 "The same or a later version is already installed"。

静态检查此前未覆盖 ProductVersion 语义（只断言 UpgradeCode / MajorUpgrade 存在性），属于"真机一测
就会暴露"的盲区。修复（preview.2 起）：

- 两个安装器 wixproj 新增 `MsiVersion = Version + "." + VersionPreview`（0.1.0-preview.2 → **0.1.0.2**）；
- MSI `Package @Version`、`UpgradeVersion @Maximum`、Bundle `@Version`、`TargetPowerToysVersion` 全部改引
  `$(var.MsiVersion)`；Bootstrapper 日志前缀同步带第四位（preview.1/2 日志不再同名难区分）；
- `check_upgrade_chain.py` 新增第 8 项断言（6 条子断言）：上述四处引用 + 两个 wixproj 的 MsiVersion 定义与
  DefineConstants 传递，防回归；
- 版本语义：preview.N → `0.1.0.N` 单调递增，已发布 preview.1（0.1.0.0）可被任何后续 preview 覆盖升级；
  stable 发布使用递增的 X.Y.Z（如 0.2.0），天然高于 preview 线。

### 6.2 真机覆盖升级实测（2026-09-30，已执行）

实测结果：**preview.1 → preview.2 候选（本地构建）覆盖升级成功**。perUser 路线，msiexec 静默安装：

- 安装已发布 preview.1 perUser MSI（ProductVersion 0.1.0，ProductCode `{A060AC30-…}`）→ 启动
  runner/Settings 生成用户数据（79 个 settings/layout/OOBE/DSC JSON 快照 + 标记文件）；
- 覆盖安装 preview.2 候选 perUser MSI（ProductVersion 0.1.0.2，ProductCode `{C56A9FF2-…}`）→
  **msiexec 退出码 0**（未修复时此路径为 1638 拒绝）；日志确认 `RemoveExistingProducts` 动作执行；
- 升级后 ARP `DisplayVersion = 0.1.0.2`，旧 ProductCode 移除、新 ProductCode 注册；
- 安装目录 runner/Settings 二进制与本地构建输出 SHA256 一致（新文件确认落盘）；
- `%LOCALAPPDATA%\PowerToysCuin` 用户数据 79/79 文件 SHA256 逐一致，标记文件保留；
- Smoke：runner 日志 `Scoobe: product_version=v0.1.0-preview.2 last_version_run=v0.1.0-preview.1`
  （升级被识别），runner + Settings + 7 个模块进程正常，用户原模块开关状态原样生效，
  `Rejected unauthenticated` 计数 0（IPC 鉴权放行正常）。

（RELEASE_CHECKLIST §4 的其余场景——OOBE 交互、右键菜单抽查、Repair/Uninstall、官方冲突——
仍按计划在 preview.2 正式发布前执行；本次自动化环境覆盖升级链核心路径。）

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
3. 小步维持：P2/P3 修复累计到值得集中发布时，按 RELEASE_CHECKLIST 走 preview.2 流程
   （升级链核心路径已于 2026-09-30 真机实测通过，见 §6.2；剩余交互场景见 RELEASE_CHECKLIST §4）；
4. 上游同步：定期评估 SYNC_GUIDE 流程，避免落后上游安全修复。
