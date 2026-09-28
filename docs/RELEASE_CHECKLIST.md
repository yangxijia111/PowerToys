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
- [ ] Bootstrapper/MSI 文件名 = `PowerToysCuin-0.1.0-preview.1-x64.exe/.msi`
- [ ] Git tag 名确认为 `v0.1.0-preview.1`（本阶段**不创建**）

## 2. 构建
- [ ] Full Release Build（`PowerToys.slnx`，x64）0 error
- [ ] `check_zh_cn_coverage.py --fail-on-orphan` 通过
- [ ] `check_fork_identity.py` 通过
- [ ] 全量 `Settings.UI.UnitTests` 通过（记录真实数字）

## 3. 安装器
- [ ] MSI（perUser + perMachine）+ Bootstrapper 构建成功
- [ ] MSI 属性验证：DisplayName=`PowerToys Cuin (Community Edition)`、Publisher=`PowerToys Cuin Community`、UpgradeCode=fork 值

## 4. 安装场景（真机）
- [ ] Clean install（静默）→ runner/Settings/模块进程启动
- [ ] Settings 修改持久化到 `%LOCALAPPDATA%\PowerToysCuin`
- [ ] OOBE / Presets 向导首次启动流程
- [ ] Repair（同版本重装）成功
- [ ] Uninstall → ARP 移除、安装目录移除、**用户配置保留**、无异常残留
- [ ] **官方冲突**：官方在场 → fork 安装被阻止（1603 + 明确文案），官方无损
- [ ] **官方后装提示**：官方在场时启动 fork → 出现"不支持同时安装"toast（可留待有官方环境的复验，需记录验证方式）
- [ ] 重启后自启动/托盘恢复（计划任务或用户配置的启动方式）

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

## 7. 发行资产
- [ ] SHA256 清单生成并记录（exe/msi）
- [ ] LICENSE（MIT）与 NOTICE.md 随包/随仓库完整
- [ ] README Preview Notice 与实际行为一致（中英同步）
- [ ] artifacts 完整性核对：文件名、大小、SHA256 与记录一致

## 8. 发布动作（需明确确认后才可执行）
- [ ] 创建 tag `v0.1.0-preview.1`
- [ ] GitHub Release（Draft → 审阅 → Publish）+ 上传 exe/msi/SHA256
- [ ] README 下载节切换为 Preview 下载说明
- [ ] 发布后： Issues 模板/公告、置顶 Known Issues（Win11 菜单限制、无签名、不与官方共存）
