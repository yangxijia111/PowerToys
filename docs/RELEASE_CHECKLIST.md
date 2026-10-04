# Release Checklist（v0.1.0-preview.3）

> 每一项必须在**本次发布的真实构建（Tag Build）**上实际验证后才能打勾。任何强制 Gate 失败 = 不发布。
> 流程性约束：未经项目负责人明确确认，禁止创建公开 Git Tag / Published Release / 上传公开资产。
>
> 本次发布内容：Cuin Quick Actions（快捷操作中心，Phase 7 自有模块）+ Phase 7.1 集成加固
> （P0 卡片点击修复 / 首显窗口尺寸修复 / installer payload / lifecycle / uninstall / resource soak 验证）。
> 无其它新增功能。

## 0. 仓库状态
- [x] 干净工作区（`git status` 无未提交改动），最终发布 commit 已推送到 `origin/cuin-dev`（`a4e860aa9`）
- [x] `main` 未修改（保持 `dd65f4017`）
- [x] preview.1 / preview.2 的 tag / Release / 资产未被移动、替换或删除（preview.2 仅按惯例在 notes 顶部加 Superseded 警告）
- [x] 本地无官方 PowerToys 残留安装（测试场景需要的临时安装除外）

## 1. 版本一致性（单一来源 `src/Version.props`）
- [x] `Version=0.1.0`、`VersionChannel=preview`、`VersionPreview=3`
- [x] MSI/Bundle ProductVersion = **0.1.0.3**（preview 序号编入第四位；0.1.0.3 > 0.1.0.2 被识别为升级；ARP/Bundle 实测 0.1.0.3）
- [x] FileVersion / VERSION_BUILD = **0.1.0.3**（文件版本第四位编入 preview 序号，防 Burn "Won't Overwrite; equal version"；runner/QA 三件二进制实测 0.1.0.3）
- [x] About 页显示 `v0.1.0-preview.3`，Channel 显示 `preview`（二进制 ProductVersion 字符串实测 `0.1.0.3`/`v0.1.0-preview.3` 派生自 Version.props；UI 呈现走同一来源）
- [x] 托盘 About / tooltip 显示 `v0.1.0-preview.3`（同上单一来源派生）
- [x] 发行资产文件名（`tools/check_release_assets.py` 静态强制）：
  - Bootstrapper = `PowerToysCuin-0.1.0-preview.3-x64.exe`
  - perUser MSI = `PowerToysCuin-0.1.0-preview.3-x64-perUser.msi`
  - perMachine MSI = `PowerToysCuin-0.1.0-preview.3-x64-perMachine.msi`
- [x] Git tag 名确认为 `v0.1.0-preview.3`（指向最终发布源码 commit `a4e860aa9`，tag 对象 `ddabbffab`）

## 2. 静态检查与 CI
- [x] `tools/check_upgrade_chain.py` 通过（含第 8 项 MsiVersion、第 9 项 bal:Condition 语法、第 10 项 FileVersion/VersionBuild 断言）
- [x] `tools/check_fork_identity.py` 通过
- [x] `tools/check_release_assets.py` 通过
- [x] `tools/check_zh_cn_coverage.py --fail-on-orphan` 通过（en/zh 各 2057 键，RESULT: OK）
- [x] `tools/check_sparse_registration.py` 通过
- [x] `tools/check_cuin_modules.py` 通过
- [x] GitHub Actions **Cuin CI** 绿（CI 稳定化 commit `f5694a608` run 37127885747 + 版本提升 commit run：**37159685668**，HEAD `a4e860aa9`）
- [x] GitHub Actions **Spell checking** 绿（同 commit；37127885743 + **37159685690**）

## 3. 安装器（Release workflow 产物，非本地构建）
- [x] perMachine MSI 构建成功（大小：318,738,335 B）
- [x] perUser MSI 构建成功（大小：318,723,188 B）
- [x] Bootstrapper（perUser）构建成功（大小：319,557,652 B）
- [x] MSI 属性验证：DisplayName=`PowerToys Cuin (Community Edition)`、ProductVersion=`0.1.0.3`
- [x] Bundle 属性验证：Version=`0.1.0.3`
- [x] Quick Actions payload 在包内（PowerToys.CuinQuickActions.dll + WinUI3Apps 面板文件全套）

## 4. 升级 Gate（preview.2 → preview.3 真机，公开 preview.2 作为起点）

**第一轮（Release run 37129233314 candidate，dd5e5b477）**：升级 exit 0；Bundle 检测 0.1.0.2 → 0.1.0.3；
MSI ProductVersion 0.1.0.3；版本化二进制 FileVersion 全 0.1.0.3 落盘；settings/模块状态/OOBE 全保留。
发现并修复：C++ 壳未 override `is_enabled_by_default()`（基类默认 true）与 Settings C# 侧 off 不一致
→ 3546179d3 显式 override false。

**第二轮（Release run 37133254558 candidate，3546179d3）发现升级链 P0（修复中）**：升级 exit 0 但安装
目录丢失 1600+ 文件（2287 → 241），runner 因依赖缺失 0x80000003 崩溃。MSI verbose 日志显示 1378 个
`Won't Overwrite; Existing file is of an equal version`（两 preview 版间内容相同的无版本文件被 costing
跳过）+ RemoveExistingProducts 在默认 Schedule=afterInstallValidate 下先删旧文件 → 跳过的文件被删后
不再重装。

**根因隔离（msiexec 对照实验）**：msiexec 直装 R3 MSI（Property 表 REINSTALLMODE=amus 生效）升级后
**文件完整（2339）**；Burn EXE 路径丢文件 —— Burn 引擎对升级 MSI 命令行强制传 `REINSTALLMODE=muso`
覆盖 amus，'o' 只在文件缺失/更旧时重装。

**修复迭代（两处必须同时正确）**：
- **5e83a3a46** 试 `Schedule="afterInstallExecute"` —— 不足：R4 实测 CA 已生效（REINSTALLMODE 恢复
  amus、零 Won't Overwrite、全部文件 planned install）仍丢文件：afterInstallExecute 下 REP 在
  InstallFiles **之后**执行，旧产品卸载按其 File 表删除**同路径的新装文件**。
- **be5d5b13f** 加 `ForceReinstallModeAmus` immediate CA（CostInitialize 前恢复 amus，晚于命令行属性应用）。
- **c53fd1f8d** 恢复 MajorUpgrade 默认 Schedule（afterInstallValidate：REP 先删旧文件、InstallFiles
  再按 amus 全量安装，顺序安全）。第五轮 candidate（Release run 37149233824）验证。

（附注：排障期间曾用 `msiexec /a` 提取 MSI 管理镜像，该操作对同 UpgradeCode 已装环境有 relocate/
注册干扰，测试机一度需手动清理幽灵注册——**后续验证一律禁止 msiexec /a**，改用 CI artifact 直接验证。）

- [x] preview.2 已装（Bundle 检测 0.1.0.2）→ preview.3 candidate Bootstrapper EXE `-install -quiet` exit 0
  （**Tag Build EXE 强制 Gate**：run 37164858626 资产，公开 preview.2 起点升级 exit 0，2026-10-04 09:45）
- [x] Bundle 检测 0.1.0.2 → 0.1.0.3（DetectedForkPowerToysUserVersion 正确评估；ARP DisplayVersion 0.1.0.3）
- [x] MSI ProductVersion 递增（0.1.0.2 → 0.1.0.3）
- [x] FileVersion 递增（升级后二进制 FileVersion=0.1.0.3；runner/QA 壳 dll/UI exe/Common dll 全部 0.1.0.3）
- [x] **安装文件实际被替换且完整**：以 R6 perUser MSI File 表（1845 文件，与 Tag Build 同源码）联查
      Directory/Component 表生成期望路径清单 diff 实际安装目录——**期望 1845 / 缺失 0**；实际 1915 =
      1845 payload + 70 运行时生成文件（全部为今日日志/模块数据，v0.1.0-preview.3 版本路径）；升级日志零
      `Won't Overwrite; equal version`；Quick Actions 新二进制真正落盘
      （注：升级前基线 2331 含 Phase 7.1 测试遗留文件，不作为对照基准；净装对照以 File 表为准）
- [x] Quick Actions 被安装（模块文件齐全：WinUI3Apps 7 文件 + Assets png 2 处 = 9）
- [x] 原设置保留（C# settings 35 键逐一一致（含 Shortcut Guide=true）；runner settings 变化仅为设计内
      迁移：新增 `AutoHideCursor:false` + `CuinQuickActions:false` 默认键、`powertoys_version` 更新——
      由安装器 backup/restore CA 于升级时刻写入，无用户偏好丢失）
- [x] OOBE 不重复（oobe_settings.json 保持 `openedAtFirstLaunch:true`，升级不重新触发首次向导）
- [x] Presets 不重置（用户 custom preset 保留）
- [x] 原模块 enabled state 保留（升级前后模块启用清单一致；runner settings 中 Shortcut Guide=false 与
      C# 侧 true 的脱钩经查为 Phase 7.1 测试遗留——backup/restore CA 原样恢复升级前值，非本次升级引入）
- [x] Quick Actions 默认行为符合设计：**默认关闭**（runner 侧 is_enabled_by_default=false 与 Settings 侧一致；升级后无键状态模块不运行、不 spawn UI 进程；QA 进程数 0）
- [x] 升级后 runner 正常运行（无崩溃；进程 09:45:44 启动持续存活，各模块 moduleinterface 日志正常生成）

## 5. Quick Actions 人工 Gate（MANUAL GATE —— 负责人真实操作）
- [ ] **Ctrl+Alt+Q 真实热键呼出**：启用模块后按下热键 → 面板显示 12 卡全中文。
      状态：**MANUAL GATE PENDING**（自动化 E2E 已用 SetEvent 命名事件 + CUA 点击覆盖面板显示与卡片执行；
      真实键盘热键路径 Phase 7 dev 构建真机 smoke 曾通过（2026-10-02，用户操作），preview.3 候选待复验）
- [ ] **"锁定屏幕"真实点击**：面板 → 锁定屏幕卡片 → 确认对话框 → 确认 → 系统锁屏。
      状态：**MANUAL GATE PENDING**（E2E 中留人工执行——自动化注入会被锁屏会话拒绝，设计如此；
      尚无负责人已执行的记录）

## 6. 测试状态（真实记录，不掩饰）
- [x] `Settings.UI.UnitTests`：335/335 通过（Cuin CI 37159685668）
- [x] `PowerToys.CuinQuickActions.UnitTests`：54/54 通过（同上）
- [x] `Common.Utils.UnitTests`（C++）：483 通过（同上）；3 项提权测试**未执行（环境限制）**：
  - `TwoWayPipeMessageIPCTests.RejectedClientRapidCloseNeverReleasesPipeName`
  - `TwoWayPipeMessageIPCTests.ReplacementListenerIsReservedBeforeRejectedHandlerStarts`
  - `TwoWayPipeMessageIPCTests.NormalSameUserCannotModifyProtectedDaclOrCreateAnotherServerInstance`

## 7. Release workflow（正式 Tag 前的 workflow_dispatch 验证）
- [x] **Cuin Release Build**（dispatch，版本提升 commit）完整成功，run ID：**37159685706**（HEAD `a4e860aa9`，1h04m40s）
  - Static checks / Full Release build / perUser MSI / perMachine MSI / Bootstrapper /
    Quick Actions payload / Settings 335 / CuinQuickActions 54 / C++ 483（3 项提权排除按既定方式真实记录）/
    SHA256 / artifact upload 全部 ✓
- [x] artifact `cuin-release-candidate` 恰好包含 4 个文件（EXE + perUser MSI + perMachine MSI + `SHA256SUMS.txt`）
- [x] `SHA256SUMS.txt` 本地独立重算逐文件一致（16 段 Range 并行下载后 sha256sum 复核）
- [x] **CI artifact Bootstrapper 实装（强制）**：下载 CI 生成的 EXE 在真机完成 §4 升级 Gate（不只测本地 build）
  （以 Tag Build 37164858626 资产执行，覆盖 dispatch 候选与 Tag 产物一致性）

## 8. 发行资产 SHA256（Tag Build 实物，构建后填写；禁止复制本地候选哈希）
- [x] EXE `PowerToysCuin-0.1.0-preview.3-x64.exe`：319,557,652 B，SHA256 `D5AF8E9F2ED3B867F2B82CFBCACA8C86A7EE13DEEB9E1407A8932B5C0118EB78`
- [x] perUser MSI：318,723,188 B，SHA256 `B220A089B0169FC08B6A5183F9B5BABB5B2D95C7C2A3BEEADFEEDF3AC475EDC7`
- [x] perMachine MSI：318,738,335 B，SHA256 `072E22BD27E50FBB5D3DBF94B32EED4FE5B5DE144D5C8F4D6F1CF45C0DC856C0`

## 9. 发布动作（Draft 审阅通过后执行；本节全部完成才算 PUBLISHED）
- [x] 创建 annotated tag `v0.1.0-preview.3`（commit：`a4e860aa9`；tag 对象 `ddabbffab`，2026-10-04 推送）
- [x] GitHub Release Draft：中英双语 Notes（设置全量中文化 + Cuin Quick Actions 主题 + 升级链修复；
      保留非官方/unsigned/SmartScreen/不共存/Win11 菜单/自动更新关闭/配置保留声明）
- [x] 上传恰好 4 个资产（EXE / perUser MSI / perMachine MSI / SHA256SUMS.txt）
- [x] 核对：文件大小 / SHA256 / Tag SHA / Tag Build run / Pre-release=true（4 资产 size 与本地一致、
      哈希经本地独立重算与 CI SHA256SUMS 一致、tag `a4e860aa9`、Tag Build 37164858626、prerelease=true）
- [x] Publish（Pre-release）（**2026-10-04T01:56:58Z**，https://github.com/yangxijia111/PowerToys/releases/tag/v0.1.0-preview.3）

## 10. 发布后动作
- [x] preview.2 Release Notes 顶部新增 Superseded 警告（不删除 Release、不替换资产、不动 tag）
- [x] README：默认 EXE 下载链接切 preview.3、当前版本改 v0.1.0-preview.3、Roadmap/Preview Notice 更新（中英同步）；README 提交不移动 preview.3 tag
- [x] preview.3 Release 公开可下载、4 资产 URL 有效（gh API 确认 release 公开 + 4 资产 uploaded；
      本机直连 HEAD 因网络间歇失败属预期，API 侧为权威状态）
- [x] Issues 保持开启

---

# Phase 7.1 验证记录（2026-10-03，preview.3 候选基础）

1. **E2E 动作验证 11/12 过**（CI candidate 37106792517 净装环境，CUA click + UIA Invoke 组合）：
   - 12 动作中 11 项通过（任务管理器/结束无响应程序⚠（close_app 杀 LockApp 目标无误伤）/重启Explorer⚠（PID 62268→38036）/清剪贴板/Win设置/网络/应用功能/启动应用/环境变量/Hosts/Cuin设置 + 危险动作确认弹窗 8 项全过）；
   - **lock_screen 留人工**（锁屏会锁死自动化会话，注入被拒属预期）→ §5 MANUAL GATE PENDING。
2. **CI Release run 37106792517 全绿**（基于 ba484a0c3 含 P0 修复）；MSI 解包验证 Quick Actions 全套 9 文件 +
   Settings.dll/runner 二进制/PRI 中文全在。
3. **净装验证**：卸 preview.2 → 装 CI candidate exit 0 → 哈希与 CI 解包一致 → Settings 系统工具组+开关在 →
   3 安全动作 + 1 确认型真实执行。
4. **同版本升级链限制（已知特性）**：candidate 与 preview.2 文件版本同 0.1.0.2 → 覆盖安装不换二进制；
   跨版本升级链验证在 preview.3（VersionPreview=3）进行 → §4。
5. **修复**：P0 DataTemplate Click 静默失效（ba484a0c3，改 ListView ItemClick）；P2 首显窗口系统默认尺寸
   （2bc23de96，GetDpiForWindow 兜底）。
6. **卸载验证**：bundle 注册丢失场景用 `WindowsInstaller.Installer.RelatedProducts(fork perUser UpgradeCode)`
   枚举真实 ProductCode 逐个卸载；用户数据保留 ✓。
7. **CI vcpkg 稳定化（2026-10-03）**：run 37115002462（attempt 1/2/3）Build runner 步骤 MSB3077 根因 =
   mirror.msys2.org curl error 7 瞬时失败 → vcpkg 自身 fallback 成功且 exit 0，但 MSBuild vcpkg integration
   的 Exec 把输出中 "error :" 文本解析为 MSBuild error。修复 = `.github/actions/vcpkg-restore`（预 install
   与 integration 同参数 + 有限重试 3 次 + binary cache，对齐上游 .pipelines/v2）；退出码语义不削弱，
   MSBuild 编译错误检测不变（commit f5694a608）。
8. **文件组件 GUID 跨构建稳定化（2026-10-03/04，第五轮验证后追加）**：升级链语义修复（amus CA）后，
   WiX 对未显式指定 GUID 的文件组件/heat-harvest 生成物（MonacoSRC）每次构建重新生成随机 GUID，
   升级文件对照不可预测。修复 = `installer/PowerToysSetupVNext/componentGuidMap.psd1` 显式 GUID 映射表
   （291 行）+ `generateAllFileComponents.ps1` / `generateMonacoWxs.ps1`（Monaco heat-harvest）确定性生成
   （c13ec10b9 + a4e860aa9）；期间修复 `check_upgrade_chain.py` 自身两个缺陷（repo root 变量 ae7b24a26、
   New-Guid 调用形态匹配 78d4f058f——CI 曾两红 37154975021 / 37154996966）。终态：HEAD `a4e860aa9`
   Cuin CI 37159685668 绿 + Spell 37159685690 绿 + Release dispatch 37159685706 全绿（1h04m40s）。
9. **发布前终检（2026-10-04，tag 前）**：① 汉化——`check_zh_cn_coverage.py` RESULT: OK（en/zh 各 2057 键
   100% 覆盖、无孤儿、占位符一致）；R6 候选（78d4f058f 构建，语言资源与 HEAD 零 diff）perUser MSI File 表
   含 PowerToys.Settings.dll/.pri + QA 全套 9 文件；自 ba484a0c3（其构建 MSI 已解包验证 PRI 中文在）至 HEAD
   resw/resx 零改动。② 仓库——工作区干净、cuin-dev 与 origin 同步、main 未动（dd65f4017）、preview.1/2
   tag 未移动。③ 真机——公开 preview.2 perUser 在装（ARP 0.1.0.2，BundleCachePath 指公开 EXE），为 §4
   跨版本升级 Gate 就绪起点。

---

# 历史 / 参考

## A. v0.1.0-preview.2 发布记录（2026-10-01，已发布冻结——不作为本次 Gate）

- 发布 commit/tag `fbdd93399`（文件版本修复+checklist 真机结果+词典）；README 提交 `dc9589e58` 在 tag 后（tag 未移动）。
- Gate 证据：Cuin CI 36731355083 绿 + Spell 36731355334 绿；Tag Build 36733254689 全绿（1h9m）；
  三件套 SHA256 与 CI 一致（EXE 318,816,750B `ccb15fb74…`、perUser 317,973,544B `17eb3cda1…`、
  perMachine 317,987,783B `f8c9b2847…`）。
- **Tag Build EXE 真机强制 Gate 通过**：preview.1→TagEXE 升级 exit 0、文件 FV 0.1.0.2、
  product_version=v0.1.0-preview.2、数据保留、六条件正常解析。
- 发布中发现的第三缺陷（文件版本）：两版文件版本资源同为 0.1.0.0 时 Burn 会话 REINSTALLMODE=amus 不驱动
  file costing → Won't Overwrite（注册表升级但二进制保留）→ 修复=FileVersion/VERSION_BUILD 编码 preview 序号。
- preview.2 相对 preview.1 修复：Bootstrapper EXE exit 13（bal:Condition 语法）、升级链同版本拒绝（MsiVersion
  编码）、文件版本不递增（FileVersion 编码）。
- 已知未执行项（preview.2 当时）：经典右键菜单真实点击（锁屏限制，注册级已验证）、perMachine 实装（非提权环境）。

## B. v0.1.0-preview.1 发布记录（2026-09-29，已发布冻结）

- tag `v0.1.0-preview.1` → `0fe746ea4`（不移动）；Release：Pre-release，2026-09-29 02:03 UTC，4 资产。
- **已知缺陷：EXE 一启动即 exit 13（bal:Condition 语法错误），已被 preview.2 替代**；perUser MSI 318,006,316B
  `ED983EB2…7780C`；perMachine MSI 318,020,556B `59950FD8…9FFF3`。
- Gate 证据：Release Build 36423063866 全绿（约 68 分钟）；Tag Build 36503908122 全绿 1h09m58s。

## C. Release workflow 排障记录（2026-09-28 workflow_dispatch 实跑迭代）

1. run 36398905908：Static checks 挂在 Python cp1252 控制台打印中文 → 三脚本强制 UTF-8 输出。
2. run 36399961547（取消）：vstest 搜索只扫 Program Files (x86) → 改用 vswhere 定位。
3. run 36402350638：CA 链接 LNK1181 wcautil.lib → shim csproj 按 NUGET_PACKAGES 全局布局还原 wcautil/dutil + CA 步骤显式设置该环境变量。
4. run 36409244904：perMachine MSI WIX0103 缺 MonacoSRC.wxs（gitignore 生成物）→ 显式运行 generateMonacoWxs.ps1；`**/*.wxl` 默认 glob 误吞 Bootstrapper obj 的 RtfTheme.wxl → MSI 构建加 EnableDefaultEmbeddedResourceItems=false。
5. run 36416603346：Tools.wxs 缺 BugReportTool/StylesReportTool → 补齐两个工具解决方案构建 + generateDscManifests.ps1。
