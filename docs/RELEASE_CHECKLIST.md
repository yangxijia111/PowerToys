# Release Checklist（v0.1.0-preview.1）

> 每一项必须在**本次发布的真实构建**上实际验证后才能打勾。任何一项失败 = 不发布。
> 流程性约束：未经项目负责人明确确认，禁止创建公开 Git Tag / Published Release / 上传公开资产。

## 0. 仓库状态
- [x] 干净工作区（`git status` 无未提交改动），HEAD `dafa3b2e4` 已推送到 `origin/cuin-dev`
- [ ] 本地无官方 PowerToys / 旧版 fork 残留安装（卸载注册表为空）

## 1. 版本一致性（单一来源 `src/Version.props`）
- [x] `Version=0.1.0`、`VersionChannel=preview`、`VersionPreview=1`（check_fork_identity / check_release_assets 静态断言）
- [ ] About 页显示 `v0.1.0-preview.1`，Channel 显示 `preview`
- [ ] 托盘 About / tooltip 显示 `v0.1.0-preview.1`
- [ ] MSI ProductVersion = `0.1.0.0`（属性表验证）
- [x] 发行资产文件名（由 `tools/check_release_assets.py` 静态强制；CI artifact 实物核对一致）：
  - Bootstrapper = `PowerToysCuin-0.1.0-preview.1-x64.exe`
  - perUser MSI = `PowerToysCuin-0.1.0-preview.1-x64-perUser.msi`
  - perMachine MSI = `PowerToysCuin-0.1.0-preview.1-x64-perMachine.msi`
- [ ] Git tag 名确认为 `v0.1.0-preview.1`（本阶段**不创建**）

## 2. 构建与 CI
- [x] Full Release Build（`PowerToys.slnx`，x64，`/restore /p:RestorePackagesConfig=true`）0 error（run 36423063866，约 55 分钟）
- [x] `check_zh_cn_coverage.py --fail-on-orphan` 通过
- [x] `check_fork_identity.py` 通过
- [x] `check_release_assets.py` 通过（perUser / perMachine 不同名防回归；ubuntu 与 windows runner 均执行）
- [x] GitHub Actions **Cuin CI** 绿（run 36402328656，2026-09-28）：Static checks ✅ / C++ runner ✅ / Settings 335/335 ✅ / C++ 483（3 项提权排除）✅
- [x] GitHub Actions **Cuin Release Build** 实跑成功（run 36423063866，2026-09-28，总时长约 68 分钟）：静态检查 / 全量构建 / 安装器 / 双 MSI / Bootstrapper / 双测试 / 资产收集全部 ✓；排障历史见 §10

## 3. 安装器（Release workflow 产物，非本地构建）
- [x] perMachine MSI 构建成功（`MachineSetup\PowerToysCuin-0.1.0-preview.1-x64-perMachine.msi`，318,000,076 字节）
- [x] perUser MSI 构建成功（`UserSetup\PowerToysCuin-0.1.0-preview.1-x64-perUser.msi`，317,985,836 字节）
- [x] Bootstrapper（perUser）构建成功（`UserSetup\PowerToysCuin-0.1.0-preview.1-x64.exe`，318,760,010 字节，内嵌 perUser MSI）
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
- [x] `Settings.UI.UnitTests`：335/335 通过（本地 Phase 4 ✅；GitHub CI run 36402328656 ✅）
- [x] `Common.Utils.UnitTests`（C++）：486 项中 483 通过（本地与 GitHub CI 一致）；以下 3 项**未执行（环境限制，非失败、非通过）**——依赖提权测试会话，GitHub 托管 runner 无法提供：
  - `TwoWayPipeMessageIPCTests.RejectedClientRapidCloseNeverReleasesPipeName`
  - `TwoWayPipeMessageIPCTests.ReplacementListenerIsReservedBeforeRejectedHandlerStarts`
  - `TwoWayPipeMessageIPCTests.NormalSameUserCannotModifyProtectedDaclOrCreateAnotherServerInstance`
  - 上游做法：在提权 agent 上经 `msbuild /t:Build;Test` 执行全部测试（`.pipelines/v2/templates/job-build-project.yml`）；fork 无法复用（GitHub 托管 runner 非提权会话）。本地（非提权）复跑同样仅此 3 项失败，确认与 GitHub 行为一致、非回归。

## 8. 发行资产（GitHub Actions artifact `cuin-release-candidate`）
- [x] artifact `cuin-release-candidate` 恰好包含 4 个文件：EXE + perUser MSI + perMachine MSI + `SHA256SUMS.txt`（已下载核对，收集步骤含缺件/多处出现/重名即失败断言）
- [x] `SHA256SUMS.txt` 使用最终发布文件名；下载后本地独立重算 SHA256 逐文件一致
- [x] 资产大小与 SHA256（run 36423063866 实物）：
  - EXE `PowerToysCuin-0.1.0-preview.1-x64.exe`：318,760,010 B，`B19E2E04835F5B2EA6F0AE55AED3CB0697951174B555E3FBC16134FFDF860AA9`
  - perUser MSI：317,985,836 B，`CFE0CC77AC273172A0D74F5A437100DE77DA3502F6B968B509F55557A6AB8D21`
  - perMachine MSI：318,000,076 B，`C7C9641B849A59565CA054945293B5400DFE3E6B00AA7EA5D19155AF74799015`
- [ ] LICENSE（MIT）与 NOTICE.md 随包/随仓库完整
- [x] README Preview Notice 与实际行为一致（中英同步，Phase 5 已修订 Branding/共存表述）

## 9. 发布动作（需明确确认后才可执行）
- [ ] 创建 tag `v0.1.0-preview.1`
- [ ] GitHub Release（Draft → 审阅 → Publish）+ 上传 EXE / perUser MSI / perMachine MSI / SHA256SUMS.txt
- [ ] README 下载节切换为 Preview 下载说明
- [ ] 发布后： Issues 模板/公告、置顶 Known Issues（Win11 菜单限制、无签名、不与官方共存）

## 10. Release workflow 排障记录（2026-09-28，workflow_dispatch 实跑迭代）
1. run 36398905908：Static checks 挂在 Python cp1252 控制台打印中文 → 三脚本强制 UTF-8 输出。
2. run 36399961547（取消）：vstest 搜索只扫 Program Files (x86) → 改用 vswhere 定位（新 run 验证）。
3. run 36402350638：CA 链接 LNK1181 wcautil.lib → shim csproj 按 NUGET_PACKAGES 全局布局还原 wcautil/dutil + CA 步骤显式设置该环境变量。
4. run 36409244904：perMachine MSI WIX0103 缺 MonacoSRC.wxs（gitignore 生成物）→ 显式运行 generateMonacoWxs.ps1；同时发现 **/*.wxl 默认 glob 误吞 Bootstrapper obj 的 RtfTheme.wxl 导致 en-us\ 子目录输出、Bootstrapper 引用失败 → MSI 构建加 EnableDefaultEmbeddedResourceItems=false（本地三件套已按最终序列全绿：根目录输出，exe/perUser/perMachine ≈318MB）。
5. run 36416603346：Tools.wxs 缺 BugReportTool/StylesReportTool → 补齐两个工具解决方案构建 + generateDscManifests.ps1（对齐上游 job-build-project.yml）。

> 注：§1 的 About/托盘版本显示与 §4/§5 真机安装场景在 Phase 3（2026-09-28，同源代码）实测通过；
> 本阶段产物为同 HEAD 的 CI 全新构建，发布前建议按 §4 快速复验一轮（约 15 分钟）。
