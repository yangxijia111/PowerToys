# Release Checklist（v0.1.0-preview.2）

> 每一项必须在**本次发布的真实构建（Tag Build）**上实际验证后才能打勾。任何强制 Gate 失败 = 不发布。
> 流程性约束：未经项目负责人明确确认，禁止创建公开 Git Tag / Published Release / 上传公开资产。
> 本次发布已获项目负责人明确授权（2026-09-30 发布 runbook）。
>
> 发布理由（准入条件 2 已满足，详见 docs/POST_RELEASE_PLAN.md §6.3/§7）：
> 1. preview.1 已发布 Bootstrapper EXE 一启动即 exit 13（Burn bal:Condition 语法错误，默认下载入口不可用）；
> 2. preview.1 → preview.2 升级链 ProductVersion 不递增（同版本拒绝，三层拦截）；
> 3. 两项均已在 cuin-dev 修复，preview.2 使用 MSI/Bundle ProductVersion **0.1.0.2**。

## 0. 仓库状态
- [ ] 干净工作区（`git status` 无未提交改动），最终发布 commit 已推送到 `origin/cuin-dev`
- [ ] `main` 未修改（保持 `dd65f4017`）
- [ ] preview.1 tag / Release / 资产未被移动、替换或删除
- [ ] 本地无官方 PowerToys / 旧版 fork 残留安装（卸载注册表为空，测试场景需要的临时安装除外）

## 1. 版本一致性（单一来源 `src/Version.props`）
- [ ] `Version=0.1.0`、`VersionChannel=preview`、`VersionPreview=2`
- [ ] MSI/Bundle ProductVersion = **0.1.0.2**（preview 序号编入第四位；preview.1 为 0.1.0.0，0.1.0.2 > 0.1.0.0 被识别为升级）
- [ ] About 页显示 `v0.1.0-preview.2`，Channel 显示 `preview`
- [ ] 托盘 About / tooltip 显示 `v0.1.0-preview.2`
- [ ] 发行资产文件名（`tools/check_release_assets.py` 静态强制）：
  - Bootstrapper = `PowerToysCuin-0.1.0-preview.2-x64.exe`
  - perUser MSI = `PowerToysCuin-0.1.0-preview.2-x64-perUser.msi`
  - perMachine MSI = `PowerToysCuin-0.1.0-preview.2-x64-perMachine.msi`
- [ ] Git tag 名确认为 `v0.1.0-preview.2`（指向最终发布源码 commit）

## 2. 静态检查与 CI
- [ ] `tools/check_upgrade_chain.py` 通过（含第 8 项 MsiVersion 断言、第 9 项 bal:Condition 语法断言）
- [ ] `tools/check_fork_identity.py` 通过
- [ ] `tools/check_release_assets.py` 通过
- [ ] `tools/check_zh_cn_coverage.py --fail-on-orphan` 通过
- [ ] `tools/check_sparse_registration.py` 通过
- [ ] GitHub Actions **Cuin CI** 绿（最终发布 commit 的 run，记录 run ID：______）
- [ ] GitHub Actions **Spell checking** 绿（同 commit）

## 3. 安装器（Tag Build 产物，非本地构建）
- [ ] perMachine MSI 构建成功（大小：______ B）
- [ ] perUser MSI 构建成功（大小：______ B）
- [ ] Bootstrapper（perUser）构建成功（大小：______ B）
- [ ] MSI 属性验证：DisplayName=`PowerToys Cuin (Community Edition)`、Publisher=`PowerToys Cuin Community`、UpgradeCode=fork 值、ProductVersion=`0.1.0.2`
- [ ] Bundle 属性验证：Version=`0.1.0.2`

## 4. 发布前真机 Gate（本地候选构建执行；Tag Build EXE 在 §7 复验）
- [ ] **EXE clean install**：无已装版本 → EXE 静默安装 exit 0 → runner/Settings/模块进程启动
- [ ] **OOBE**：全新用户状态首启出现向导 → 选择 Preset → 推荐工具正确显示 → Apply 成功 → 第二次启动不重复 OOBE
- [ ] **zh-CN UI**：中文界面真实渲染（General / Dashboard / Presets 页抽查，非仅 PRI 资源验证）
- [ ] **经典右键菜单实际点击**：PowerRename / Image Resizer / File Locksmith 可用
- [ ] **EXE 覆盖升级**：preview.1 已装 → preview.2 EXE `-install -quiet` exit 0 → Burn 日志无 `Failed to parse condition`、六个 bal:Condition 正常解析、`DetectedForkPowerToysUserVersion=0.1.0`、`TargetPowerToysVersion=0.1.0.2`
- [ ] **MSI 覆盖升级**：preview.1 → preview.2 MSI exit 0，RemoveExistingProducts 执行，用户数据保留
- [ ] **Repair**（同版本维护模式重装）exit 0
- [ ] **Uninstall**：ARP/安装目录移除、用户配置保留、无异常残留
- [ ] **官方冲突**：官方在场 → preview.2 EXE 阻止安装（文案正确），官方版本不被破坏
- [ ] **perMachine**：管理员环境 clean install / 启动 / Settings / uninstall / ARP 与目录清理 / 用户配置行为（若环境无法提权，如实记录为未执行）
- [ ] **restart / persistence**：完整重启或等价登录流程后 runner/托盘/设置持久化符合预期（若无法重启，机制级验证并如实记录）
- [ ] Settings 修改持久化到 `%LOCALAPPDATA%\PowerToysCuin`
- [ ] Runner 日志无 `Rejected unauthenticated Settings pipe client`
- [ ] Runner 日志确认后台 PeriodicUpdateWorker 未启动
- [ ] 「检查更新」/「立即更新」= 打开 GitHub Releases 页（不下载不执行安装包）

## 5. 功能抽查
- [ ] Presets 应用（5 场景之一，经 OOBE 或 Settings）
- [ ] File Locksmith（经典右键菜单）
- [ ] PowerRename（经典右键菜单）
- [ ] Image Resizer（经典右键菜单）
- [ ] zh-CN 界面抽查（设置页 + 至少 2 个模块）

## 6. 测试状态（真实记录，不掩饰）
- [ ] `Settings.UI.UnitTests`：335/335 通过
- [ ] `Common.Utils.UnitTests`（C++）：486 项中 483 通过；3 项提权测试**未执行（环境限制）**：
  - `TwoWayPipeMessageIPCTests.RejectedClientRapidCloseNeverReleasesPipeName`
  - `TwoWayPipeMessageIPCTests.ReplacementListenerIsReservedBeforeRejectedHandlerStarts`
  - `TwoWayPipeMessageIPCTests.NormalSameUserCannotModifyProtectedDaclOrCreateAnotherServerInstance`

## 7. Tag Build 与 EXE 实物验证（强制 Gate）
- [ ] Tag `v0.1.0-preview.2` 指向最终发布 commit 并已推送
- [ ] **Cuin Release Build**（tag 触发）完整成功，run ID：______
  - Static checks / Full Release build / perUser MSI / perMachine MSI / Bootstrapper / Settings 335 / C++ 483（3 项提权排除按既定方式真实记录）/ SHA256 / artifact upload 全部 ✓
- [ ] artifact `cuin-release-candidate` 恰好包含 4 个文件（EXE + perUser MSI + perMachine MSI + `SHA256SUMS.txt`）
- [ ] `SHA256SUMS.txt` 本地独立重算逐文件一致
- [ ] **Tag Build EXE 真机验证（强制）**：preview.1 已装 → 运行 Tag Build EXE → 升级 exit 0 → 启动 runner/Settings → 版本显示 preview.2 → 用户配置保留
- [ ] MSI 属性复核（Tag Build 实物）：ProductVersion=0.1.0.2、UpgradeCode=fork 值

## 8. 发行资产 SHA256（Tag Build 实物，构建后填写；禁止复制本地候选哈希）
- [ ] EXE `PowerToysCuin-0.1.0-preview.2-x64.exe`：______ B，SHA256 ______
- [ ] perUser MSI：______ B，SHA256 ______
- [ ] perMachine MSI：______ B，SHA256 ______

## 9. 发布动作（Draft 审阅通过后执行；本节全部完成才算 PUBLISHED）
- [ ] 创建 annotated tag `v0.1.0-preview.2`（commit：______）
- [ ] GitHub Release Draft：中英双语 Notes（修复重点：Bootstrapper EXE exit 13、升级链 ProductVersion、preview.1 用户应升级的醒目提示；保留非官方/unsigned/SmartScreen/不共存/Win11 菜单/自动更新关闭/配置保留声明）
- [ ] 上传恰好 4 个资产（EXE / perUser MSI / perMachine MSI / SHA256SUMS.txt）
- [ ] 核对：文件大小 / SHA256 / Tag SHA / Tag Build run / Pre-release=true
- [ ] Publish（Pre-release）

## 10. 发布后动作
- [ ] preview.1 Release Notes 顶部新增 Superseded 警告（不删除 Release、不替换资产、不动 tag）
- [ ] README：默认 EXE 下载链接切 preview.2、当前版本改 v0.1.0-preview.2、Roadmap/Preview Notice 更新、注明 preview.1 已因 installer 缺陷被替代（中英同步）；README 提交不移动 preview.2 tag
- [ ] preview.2 Release 公开可下载、4 资产 URL 有效
- [ ] Issues 保持开启

---

# 历史 / 参考（v0.1.0-preview.1，已发布冻结——不作为本次 Gate）

## A. preview.1 发布记录（2026-09-29）

- tag `v0.1.0-preview.1` → `0fe746ea4`（不移动）；Release：Pre-release，2026-09-29 02:03 UTC 发布，4 资产：
  - EXE 318,767,976B `4E4E7061…93D8E`（**已知缺陷：一启动即 exit 13，已被 preview.2 替代**）
  - perUser MSI 318,006,316B `ED983EB2…7780C`
  - perMachine MSI 318,020,556B `59950FD8…9FFF3`
  - SHA256SUMS.txt
- 发布时 Gate 证据：Full Release Build run 36423063866（约 68 分钟）全绿；Cuin CI run 36402328656（Settings 335/335、C++ 483）；Tag Build run 36503908122 全绿 1h09m58s；本地独立重算 SHA256 一致。
- Phase 3（2026-09-28，同源代码）真机场景实测通过：官方在场装 fork → 1603 阻止；fork 在场装官方 → 官方成功但 runner 同名 mutex 互斥。

## B. preview.1 的已知缺陷（preview.2 修复对象）

1. **Bootstrapper EXE exit 13**（docs/POST_RELEASE_PLAN.md §6.3）：bal:Condition 操作符大写 + 字面量未加引号，Burn 条件解析失败。所有 preview.1 真机安装当时都走 msiexec，EXE 路径零覆盖，发布后才暴露。
2. **升级链同版本拒绝**（§6.1）：preview.1 与后续版本 ProductVersion 同为 0.1.0，MajorUpgrade 1638 / UpgradeVersion 检测失效 / bal:Condition `>=` 拦截，三层全部挡住覆盖升级。

## C. Release workflow 排障记录（2026-09-28，workflow_dispatch 实跑迭代，preview.2 沿用）

1. run 36398905908：Static checks 挂在 Python cp1252 控制台打印中文 → 三脚本强制 UTF-8 输出。
2. run 36399961547（取消）：vstest 搜索只扫 Program Files (x86) → 改用 vswhere 定位。
3. run 36402350638：CA 链接 LNK1181 wcautil.lib → shim csproj 按 NUGET_PACKAGES 全局布局还原 wcautil/dutil + CA 步骤显式设置该环境变量。
4. run 36409244904：perMachine MSI WIX0103 缺 MonacoSRC.wxs（gitignore 生成物）→ 显式运行 generateMonacoWxs.ps1；`**/*.wxl` 默认 glob 误吞 Bootstrapper obj 的 RtfTheme.wxl → MSI 构建加 EnableDefaultEmbeddedResourceItems=false。
5. run 36416603346：Tools.wxs 缺 BugReportTool/StylesReportTool → 补齐两个工具解决方案构建 + generateDscManifests.ps1。
