# PowerToys Cuin 项目交接文档（HANDOFF）

> 本文档面向接手本项目的**人类开发者或 AI 智能体**，提供从零理解项目现状、继续开发与发布的全部关键信息。
> 最后更新：2026-10-04（v0.1.0-preview.3 发布时点）。
> 配套文档索引见 §12；本文与仓库内其他文档冲突时，以本文 + `docs/RELEASE_CHECKLIST.md` 为准。

---

## 1. 项目是什么

**PowerToys Cuin (Community Edition)** 是 [microsoft/PowerToys](https://github.com/microsoft/PowerToys) 的社区二开发行版，核心价值：

1. **设置界面全量中文化**（上游 0.101 新设置 UI 无官方中文；本 fork 补齐全部 2057 键，zh-CN 覆盖率 100%）+ 中/EN 一键切换；
2. **自有功能模块 CuinQuickActions 快捷操作中心**（Ctrl+Alt+Q 呼出 12 项系统快捷操作）；
3. 预置优化（Presets / OOBE 预设选择器）、双语 README、独立发行身份（与官方互斥安装）。

- 上游基线：microsoft/PowerToys main（约 0.101 时代，net10.0-windows10.0.26100.0）
- Fork 仓库：`https://github.com/yangxijia111/PowerToys`
- 开发分支：**`cuin-dev`**（所有开发合入这里；`main` 保留跟随上游，**不要在 main 上开发**）
- 功能分支：`feature/*`（会触发 Cuin CI，见 `cuin-ci.yml` trigger 配置）

## 2. 发布版本史

| 版本 | 日期 | tag 指向 | 状态 | 要点 |
|---|---|---|---|---|
| v0.1.0-preview.1 | 2026-09-29 | `0fe746ea4` | **已废弃** | 首发版；EXE 有 bal:Condition 语法缺陷（启动即 exit 13），仅 MSI 可用 |
| v0.1.0-preview.2 | 2026-10-01 | `fbdd93399` | 已被 preview.3 取代 | 修 EXE 语法/同版本拒绝/文件版本三缺陷 |
| v0.1.0-preview.3 | 2026-10-04 | `a4e860aa9` | 当前 | 全量中文化 + Quick Actions 模块 + 升级链丢文件 P0 修复 |

**版本号规则**（单一来源 `src/Version.props`）：`0.1.0` = MSI ProductVersion 四段的前三段；`VersionPreview` = preview 序号（3）→ 派生：
- Bundle/About 显示 `v0.1.0-preview.3`
- MSI ProductVersion = `0.1.0.3`（MsiVersion 属性）
- **二进制 FileVersion = `0.1.0.3`**（VERSION_BUILD 编码 preview 序号——必须递增，否则 Burn 会话下同版本文件 "Won't Overwrite"，注册表升级但二进制保留）

**改版本时同步**：`Version.props` 的 `VersionPreview` + `check_release_assets.py` 里 workflow 期望清单（CI 有断言）。

## 3. 身份隔离（改安装器前必读）

fork 与官方 PowerToys **互斥安装、不支持共存**（COM CLSID/协议/AUMID 等保留官方值，无法并存）。fork 自有身份：

| 项 | 值 |
|---|---|
| 安装目录（perUser） | `%LOCALAPPDATA%\PowerToysCuin` |
| UpgradeCode perUser / perMachine | `DA33EB25-63A5-4E9A-8B04-D8AE81BB888D` / `78975C14-…`（见 IDENTITY_MAP） |
| Bundle UpgradeCode | `0A739D71-…` |
| AppData 数据根（runner 真源） | `%LOCALAPPDATA%\PowerToysCuin\`（C++ 唯一来源 `shared_constants.h`） |
| 设置文件（**两个**，见 §9 坑） | runner: `%LOCALAPPDATA%\PowerToysCuin\settings.json`；Settings C# EnabledModules: `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` |

- 官方在场装 fork → MSI Launch Condition 1603 阻止；fork 在场装官方 → runner 同名 mutex 互斥。
- 自更新已禁用（`UpdateUtils.cpp` `FORK_AUTO_UPDATE_INSTALL_ENABLED=false`）。
- 验证工具：`tools/check_fork_identity.py`、`tools/check_upgrade_chain.py`、`Settings.UI.UnitTests/ForkIdentityTests.cs`（都在 CI 强制）。

## 4. 中文化体系

- **资源文件**：`src/settings-ui/Settings.UI/Strings/{en-us,zh-CN}/Resources.resw`（主文件 2057 键）+ `Settings.UI.Controls/Strings/…`。zh-CN 100% 覆盖，无回退。
- **交付形式**：中文以 ZH-CN 限定符**嵌在主 PRI 内**（不是 zh-CN 卫星目录）。
- **翻译工具链**：工作区 `i18n_tools/`（**在仓库外**，PowerToys 上级目录）：`extract_missing.py`（提缺键）→ 分批翻译 → `merge_resw.py`（校验占位符 `{N}`/`%s` 数量、`\r\n`、XML 转义、键集合后合并）。术语表 `TRANSLATE_RULES.md`（保持唤醒/窗口分区/取色器/速览/批量重命名/文字提取（OCR）等既有译名）。
- **覆盖率检查**：`tools/check_zh_cn_coverage.py`（CI 强制，当前 RESULT: OK）。
- **运行时语言切换链路**（中↔EN 一键切换）：Settings 页脚按钮（ShellPage，图标 E774）→ 读 `language.json`（不存在则跟随系统 zh 判定）→ 发 `{"language":"xx"}` IPC → runner `settings_window.cpp` "language" 分支写文件 → 提示重启 → "立即重启" = `ActionMessage("restart_maintain_elevation")`。各 WinUI3 模块 App 构造函数经 `LanguageHelper.LoadLanguage` → `PrimaryLanguageOverride`/`CurrentUICulture` 生效。
- **en-us resw 增量修改纪律**：新键做**文本级插入**（勿整文件重写，会丢 ResX 头注释并放大与上游 diff）。
- **遗留**：模块编辑器独立进程（FancyZonesEditor 等卫星 resx）仍英文——后续增量汉化。

## 5. CuinQuickActions 模块（自有功能，Peek 模型）

```
src/modules/cuin/cuinquickactions/        C++ 壳（PowerToys.CuinQuickActions.dll，热键/IPC/生命周期）
src/modules/cuin/CuinQuickActions.Common/ 纯逻辑（ActionCatalog 静态动作目录 / QuickActionRunner 危险确认闸 / ForegroundProcessGuard 系统进程保护名单）
src/modules/cuin/CuinQuickActions.UI/     WinUI3 面板（PowerToys.CuinQuickActions.UI.exe）
```

- 热键 Ctrl+Alt+Q；12 动作；危险动作（结束程序/重启 Explorer/锁屏等）二次确认。
- **默认关闭**：三处必须一致——Settings C# 默认 off + C++ 壳 `is_enabled_by_default()` override **false**（3546179d3，基类默认 true 会不一致）+ 无键时模块不运行。
- IPC = 命名事件（事件名两侧字面量由 `tools/check_cuin_modules.py` 校验，CI 强制）。
- 单测：`PowerToys.CuinQuickActions.UnitTests` 54 个。
- 新模块开发指南：**`docs/CUSTOM_MODULE_GUIDE.md`**（含 Installer Gate：Full Build 成功 ≠ 安装器包含模块，必须过 `check_cuin_modules.py` + MSI File 表验证）。

**WinUI3 unpackaged 模块四大坑**（新面板类模块通用）：
1. 资源名是 `AccentFillColorDefaultBrush`（没有 `AccentFillDefaultBrush`，用错 = 窗口创建即 XamlParse 崩 0xc000027b 且绕过 UnhandledException）；
2. `new Windows.ApplicationModel.Resources.ResourceLoader()` 在 unpackaged 抛 FileNotFound → 用 WinAppSDK 的 `ResourceLoader("模块.pri")`；
3. Window 根级与 DataTemplate 内 **x:Bind 均不可用** → 经典 Binding + DataContext；DataTemplate 内 `Click="handler"` 也静默失效 → `ListView.IsItemClickEnabled + ItemClick`（P0 教训 ba484a0c3）；
4. 面板失焦 Close → 进程退出是**设计内**（Peek 同款），下次热键 runner 重新拉起。

## 6. 构建与 CI

### CI（`.github/workflows/`）

| workflow | 触发 | 作用 |
|---|---|---|
| `cuin-ci.yml` | push cuin-dev / feature/* | 静态检查族 + 全解决方案构建 + 单测（Settings 335 / QA 54 / C++ 483，其中 3 个提权 pipe 测试环境限制标注未执行） |
| `cuin-release.yml` | workflow_dispatch / push `v*` tag | 完整安装器三件套构建 → artifact `cuin-release-candidate`（EXE + 双 MSI + SHA256SUMS.txt）。**只产 artifact，不自动发布** |
| `spelling2.yml` | push | check-spelling，`only_check_changed_files: 1`；词典 `.github/actions/spell-check/expect.txt` |

- **静态检查工具族**（Python，改安装器/模块/版本后本地先跑一遍）：`check_fork_identity.py`、`check_upgrade_chain.py`、`check_release_assets.py`、`check_cuin_modules.py`、`check_sparse_registration.py`、`check_zh_cn_coverage.py`。
- **vcpkg 稳定化**：`.github/actions/vcpkg-restore`（composite action）——预跑与 MSBuild integration 完全同参数的 vcpkg install（同 manifest/triplet **含结尾反斜杠**）+ 有限重试 + `%LOCALAPPDATA%\vcpkg\archives` binary cache。背景：mirror.msys2.org 瞬时失败会被 MSBuild Exec 解析成 MSB3077 假失败。
- Release 构建顺序与上游 .pipelines 对齐：主构建 → DSC manifests → BugReportTool/StylesReportTool 两 sln → publish.cmd → CmdPal → CA/BA（wcautil 全局布局还原）→ perMachine MSI → perUser MSI → Bootstrapper。已知不可设 `IsPipeline=1`（激活 21 语言 loc 块，fork 无上游 loc 产物）。

### 本地构建（VS2026 Community @ D:\Develop\VS_2026）

- 关键参数：`VsDevCmd.bat -arch=x64` + PATH 前置 pwsh shim（`D:\Develop\pwsh-shim`）+ `_CL_=/MP2` + `MSBUILDDISABLENODEREUSE=1` + `/m:2`（内存 27.7GB，再高 OOM）。
- msbuild 参数传 cmd 时用 `-` 前缀（MSYS 会把 `/` 转路径）。
- 构建产物 ~90GB，D 盘紧张——**构建前报告体积**（用户磁盘极度敏感）。
- 本地完整安装器脚本参考：工作区上级 `build_preview2_local.cmd`（ASCII 注释，GBK 代码页下中文注释会破坏 cmd）。

## 7. 发布 Runbook（浓缩版）

> 完整版 `docs/RELEASE_CHECKLIST.md`（当前就是 preview.3 模板）。流程 = preview.2/3 两次实跑验证过的路径。

1. **版本提升**：`Version.props` VersionPreview+1 → commit → Cuin CI + Spell 双绿；
2. **workflow_dispatch 跑 Cuin Release Build** → 全绿 → 下载 artifact 做候选验证（升级 Gate 的主要轮次用 dispatch 候选做，失败好回头）；
3. **升级 Gate**（§4 of checklist，真机，公开前版 → 新候选 EXE `-install -quiet`）：exit 0 / 版本链（Bundle+MSI+FV 三层都递增）/ **安装文件数与净装一致**（防丢文件 P0 复发）/ settings+模块状态+OOBE+Presets 保留 / QA 默认关闭 / runner 正常；
4. 全过后 **打 annotated tag → push → Tag Build**（push v* 自动触发）→ 下载三件套 + SHA256SUMS → 本地独立重算哈希；
5. **Tag EXE 真机强制 Gate**（从已装旧版跨版本升级，验证的就是发布资产）；
6. `gh release create --draft`（notes 模板在工作区 `release_notes_v*.md`，双语）→ 逐项核验 → `gh release edit --draft=false` **Publish 为 Pre-release**；
7. 发布后：前版 release notes 顶部加 Superseded 警告（不动前版 tag/资产）；README 下载入口/当前版本/Roadmap 更新（**提交在 tag 之后，不移动 tag**）；push；
8. 公开资产 URL HEAD 200 核验 + Issues 开启确认。

**资产下载坑**：`gh run download` 会挂死、单流 45KB/s → 用 `curl -L` 拿 redirect_url 后 **Range 并行分段下载 + 仅 206 追加防污染**（工作区 `download_p3_artifact.sh` 是现成实现）。

## 8. 安装器升级链（WiX，动 Product.wxs 前必读）

已修复的三代缺陷（都有 `check_upgrade_chain.py` 防回归断言）：

1. **preview.1**：Bootstrapper bal:Condition 语法——操作符必须小写 `and`/`or`（与 MSI Condition 相反）；version 字面量必须带引号 `v >= "0.0.0.0"`。
2. **preview.2**：同版本拒绝（MsiVersion 编码）+ 文件版本不递增（VERSION_BUILD 编码）。
3. **preview.3（最大 P0）**：**Burn 引擎对升级 MSI 强制传 `REINSTALLMODE=muso`**，覆盖 Property 表的 amus → 'o' 语义下内容相同的无版本文件被 costing 跳过 → RemoveExistingProducts（默认 Schedule=afterInstallValidate）先删旧文件 → 升级 exit 0 但 1600+ 文件丢失、runner 崩。**修复 = `ForceReinstallModeAmus` immediate CA 在 CostInitialize 前恢复 amus**（be5d5b13f），保持默认 Schedule（afterInstallExecute 方案被 R4 实测否决：REP 在 InstallFiles 之后按旧 File 表删同路径新装文件）。
4. **GUID 稳定化**：文件组件 GUID 必须跨构建稳定 → `installer/PowerToysSetupVNext/componentGuidMap.psd1`（显式 GUID 映射表，291 行）+ `generateAllFileComponents.ps1` / `generateMonacoWxs.ps1`（Monaco heat-harvest 用确定性 GUID 生成）。

**验证纪律**：验证 MSI 内容用 PowerShell `WindowsInstaller.Installer` COM 读 File/Property 表（`Read-MsiTable` 模式见工作区 `verify_p3_msi.ps1`；Record 取值用 `StringData` 属性，`StringProperty` 在 late-binding 下不可用）。**绝对禁止 `msiexec /a`**——对同 UpgradeCode 已装环境会 relocate/污染注册，留幽灵 ProductCode 导致 0x81f40001 拦截。

**卸载**：用 Bundle EXE `-uninstall`（msiexec 卸 MSI 不清 bundle 注册）。ARP 幽灵键时用 `WindowsInstaller.Installer.RelatedProducts(fork UpgradeCode)` 枚举真 ProductCode 逐个 `/x`。

## 9. 测试体系与真机验证方法论

- 单测：Settings 335 / QA 54 / C++ Common 483（3 个提权测试设计上需提权 server，非提权环境必失败，非回归）。
- **E2E 自动化可靠组合**（锁屏/注入限制下踩出来的）：CUA click 点 WinUI 卡片 + PowerShell UIA Invoke 点标准对话框按钮 + `SetEvent` 命名事件显面板 + 直启 exe 等价 relaunch；CUA `elements()` 与 `click()` 之间不能插耗时调用（UIA 树漂移）。
- 不可自动化（留 MANUAL GATE，由负责人手动）：真实键盘热键、锁屏动作（注入被锁屏会话拒绝，设计如此）。
- **runner 真源 settings 是 `%LOCALAPPDATA%\PowerToysCuin\settings.json`**（IPC 写入、QA 开关在这）；Settings C# 的 EnabledModules 落在 `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`。验证"设置保留"要认准 runner 真源；测试写入 runner settings 的临时键测前要清。
- runner 启动会自重启换 PID；升级后 runner 检测磁盘版本≠运行版本会弹 toast 并退出（正常行为勿误判）；判断存活用进程名+Path 过滤（orphan 子进程 Path 为空按 Name 抓）。
- PS 脚本坑：PS5.1 `Set-Content -Encoding UTF8` 带 BOM → runner `json::from_file` 不容 BOM（settings 整体解析失败回退全模块默认启用）；无三元运算符；`Get-Process | Where Path` 偶发 null。
- 静默安装别用 Git Bash 裸转参（msiexec 弹 UI 卡住）→ PowerShell `Start-Process` 传参。
- 锁屏下 GUI 验证用 UIA（Invoke/Select 可用，WinUI 页面导航后树重建需重查）；UIA Invoke 对 islands 按钮 E_FAIL（点击只能人工）。

## 10. 当前已知限制 / 遗留工作

1. **安装包未签名**：SmartScreen "未知发布者"。签名基建已就绪（`tools/fork_sign_assets.ps1` 两阶段签名 + `ForkSigning.props` + cuin-release.yml 可选 secrets 门控步骤），拿到代码签名证书后按 `docs/CODE_SIGNING.md` §5/§6 接入；签名后 sparse MSIX 会自动恢复 Win11 新式右键菜单。
2. 模块编辑器（FancyZonesEditor 等卫星 resx）未汉化。
3. 同版本覆盖安装不换二进制（文件版本相等时 MSI costing 跳过）——**已知特性**，跨版本升级正常；测试升级链必须跨 preview 序号。
4. 自动更新禁用（设计）：更新端点指向 fork 但 `FORK_AUTO_UPDATE_INSTALL_ENABLED=false`，手动下载覆盖升级。
5. 上游 main 持续演进，同步窗口拉大风险——见 §11。
6. QA 的 lock_screen / 真实热键两项 MANUAL GATE 每个版本需负责人手测一次。

## 11. 上游同步策略

- 方式：`git merge upstream/main` 到 cuin-dev。
- **热点文件**（几乎必冲突，按 fork 侧意图解）：`Settings.UI/ShellPage.xaml` > `Strings/en-us/Resources.resw`（文本级插入法，见 §4）> `[fork-identity]` 锚点标记的身份文件 > `ModuleHelper/EnabledModules`（新模块注册 + QA）> `App.xaml.cs`（语言初始化）> `GeneralPage*`（中文化）。
- fork 侧改动大多带 `[fork-brand]` / `[fork-identity]` 锚点注释，`git grep fork-brand` 可快速定位全部自有改动面。
- 详细冲突预案：`docs/SYNC_GUIDE.md`（§1.1 身份文件清单 / §1.2 新模块接入）。

## 12. 文档地图

| 文档 | 内容 |
|---|---|
| `docs/RELEASE_CHECKLIST.md` | 发布 runbook 全文 + 历史 run 排障记录（v0.1.0-preview.1/2/3） |
| `docs/CODE_SIGNING.md` | 签名架构与两种模式手册 |
| `docs/CUSTOM_MODULE_GUIDE.md` | 新模块开发全流程（Peek 模型）+ Installer Gate |
| `docs/SYNC_GUIDE.md` | 上游同步与冲突预案 |
| `docs/IDENTITY_AUDIT.md` / `IDENTITY_MAP.md` | 发行身份审计与 GUID 对照表 |
| `docs/DEVELOPMENT_PLAN.md` / `POST_RELEASE_PLAN.md` | 阶段开发计划 |
| `installer/PowerToysSetupVNext/` | WiX 工程（Product.wxs 等，注意 `[fork-identity]` 锚点） |
| 工作区（仓库外）`i18n_tools/` | 翻译提取/合并工具 + 术语表 |
| 工作区（仓库外）`release_notes_v*.md` | 各版本 release notes 草稿（双语） |

## 13. 接手人 Quick Start

1. `git clone` fork → `git checkout cuin-dev`；确认 CI 最近双绿；
2. 装 VS2026 Community（组件清单见 `\.github/workflows/cuin-release.yml` 还原步骤 + 上游 vsconfig；UWP 组件必须用 vsconfig 原始 ID `Microsoft.VisualStudio.ComponentGroup.UWP.VC`，Community 上缩写 ID 是 Non-installable）；
3. 本地构建按 §6 参数；磁盘预算 ~90GB，**先向负责人确认磁盘余量**；
4. 跑 `tools/check_*.py` 全族确认基线绿；
5. 开发规范遵循上游 `AGENTS.md` + `doc/devdocs/`（本 fork 追加：注释与提交信息可用中文，StyleCop 全局强制——标准三行版权头/一文件一类型/SA1515 空行规则）；
6. 发布前完整走 §7 runbook，不跳 Gate；
7. 有疑问先读 `docs/RELEASE_CHECKLIST.md` 的历史记录——大部分坑已在里面付过学费。

---

*本交接文档由项目负责人在 v0.1.0-preview.3 发布时点整理。项目级历史决策与逐 Phase 详情见仓库 git log（cuin-dev 分支，提交信息均为原子化描述）。*
