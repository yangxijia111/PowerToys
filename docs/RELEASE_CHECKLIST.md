# Release Checklist（v0.1.0-preview.1）

> 每一项必须在**本次发布的真实构建**上实际验证后才能打勾。任何一项失败 = 不发布。
> 流程性约束：未经项目负责人明确确认，禁止创建公开 Git Tag / Published Release / 上传公开资产。

## 0. 仓库状态
- [ ] 干净工作区（`git status` 无未提交改动），HEAD 已推送到 `origin/cuin-dev`
- [ ] 本地无官方 PowerToys / 旧版 fork 残留安装（卸载注册表为空）

## 1. 版本一致性（单一来源 `src/Version.props`）
- [ ] `Version=0.1.0`、`VersionChannel=preview`、`VersionPreview=1`
- [ ] About 页显示 `v0.1.0-preview.1`，Channel 显示 `preview`
- [ ] 托盘 About / tooltip 显示 `v0.1.0-preview.1`
- [ ] MSI ProductVersion = `0.1.0.0`（属性表验证）
- [ ] 发行资产文件名（由 `tools/check_release_assets.py` 静态强制）：
  - Bootstrapper = `PowerToysCuin-0.1.0-preview.1-x64.exe`
  - perUser MSI = `PowerToysCuin-0.1.0-preview.1-x64-perUser.msi`
  - perMachine MSI = `PowerToysCuin-0.1.0-preview.1-x64-perMachine.msi`
- [ ] Git tag 名确认为 `v0.1.0-preview.1`（本阶段**不创建**）

## 2. 构建与 CI
- [ ] Full Release Build（`PowerToys.slnx`，x64，`/restore /p:RestorePackagesConfig=true`）0 error
- [ ] `check_zh_cn_coverage.py --fail-on-orphan` 通过
- [ ] `check_fork_identity.py` 通过
- [ ] `check_release_assets.py` 通过（perUser / perMachine 不同名防回归）
- [ ] GitHub Actions **Cuin CI**（windows-latest）绿：runner 构建 + Settings 测试 + C++ 测试
- [ ] GitHub Actions **Cuin Release Build**（workflow_dispatch 实跑）绿：全量构建 + 安装器 + 测试 + 静态检查

## 3. 安装器（Release workflow 产物，非本地构建）
- [ ] perMachine MSI 构建成功（`MachineSetup\…-perMachine.msi`）
- [ ] perUser MSI 构建成功（`UserSetup\…-perUser.msi`）
- [ ] Bootstrapper（perUser）构建成功（`UserSetup\…-x64.exe`，内嵌 perUser MSI）
- [ ] MSI 属性验证：DisplayName=`PowerToys Cuin (Community Edition)`、Publisher=`PowerToys Cuin Community`、UpgradeCode=fork 值

## 4. 安装场景（真机，v0.1.0-preview.1 RC 已验证；发布前如时间允许复验）
- [ ] Clean install（静默）→ runner/Settings/模块进程启动
- [ ] Settings 修改持久化到 `%LOCALAPPDATA%\PowerToysCuin`
- [ ] OOBE / Presets 向导首次启动流程
- [ ] Repair（同版本重装）成功
- [ ] Uninstall → ARP 移除、安装目录移除、**用户配置保留**、无异常残留
- [ ] **官方冲突**：官方在场 → fork 安装被阻止（1603 + 明确文案），官方无损
- [ ] **官方后装提示**：官方在场时启动 fork → 出现"不支持同时安装"toast
- [ ] 重启后自启动/托盘恢复（计划任务或用户配置的启动方式）

> 以上 4/5 两节在 Phase 3 场景实测（2026-09-28）已全部通过；此处保留为发布前复验清单。

## 5. 功能抽查
- [ ] Presets 应用（5 场景之一）
- [ ] File Locksmith（经典右键菜单）
- [ ] PowerRename（经典右键菜单）
- [ ] Image Resizer（经典右键菜单）
- [ ] zh-CN 界面抽查（设置页 + 至少 2 个模块）

## 6. 安全与更新
- [ ] Runner 日志无 `Rejected unauthenticated Settings pipe client`（IPC 鉴权放行正常）
- [ ] 「检查更新」/「立即更新」行为 = 打开 GitHub Releases 页（不下载不执行任何安装包）
- [ ] Runner 日志确认后台 PeriodicUpdateWorker 未启动

## 7. 测试状态（真实记录，不掩饰）
- [ ] `Settings.UI.UnitTests`：335/335 通过（本地 + GitHub CI 各记录一次）
- [ ] `Common.Utils.UnitTests`（C++）：486 项中 483 通过；以下 3 项**未执行（环境限制，非失败、非通过）**——依赖提权测试会话，GitHub 托管 runner 无法提供：
  - `TwoWayPipeMessageIPCTests.RejectedClientRapidCloseNeverReleasesPipeName`
  - `TwoWayPipeMessageIPCTests.ReplacementListenerIsReservedBeforeRejectedHandlerStarts`
  - `TwoWayPipeMessageIPCTests.NormalSameUserCannotModifyProtectedDaclOrCreateAnotherServerInstance`
  - 上游做法：在提权 agent 上经 `msbuild /t:Build;Test` 执行全部测试（`.pipelines/v2/templates/job-build-project.yml`）；fork 无法复用（GitHub 托管 runner 非提权会话）。本地（非提权）复跑同样仅此 3 项失败，确认与 GitHub 行为一致、非回归。

## 8. 发行资产（GitHub Actions artifact `cuin-release-candidate`）
- [ ] artifact 恰好包含 4 个文件：EXE + perUser MSI + perMachine MSI + `SHA256SUMS.txt`（缺任一 workflow 即失败）
- [ ] `SHA256SUMS.txt` 使用最终发布文件名；哈希逐文件核对
- [ ] 记录资产大小与 SHA256（见发布记录）
- [ ] LICENSE（MIT）与 NOTICE.md 随包/随仓库完整
- [ ] README Preview Notice 与实际行为一致（中英同步）

## 9. 发布动作（需明确确认后才可执行）
- [ ] 创建 tag `v0.1.0-preview.1`
- [ ] GitHub Release（Draft → 审阅 → Publish）+ 上传 EXE / perUser MSI / perMachine MSI / SHA256SUMS.txt
- [ ] README 下载节切换为 Preview 下载说明
- [ ] 发布后： Issues 模板/公告、置顶 Known Issues（Win11 菜单限制、无签名、不与官方共存）
