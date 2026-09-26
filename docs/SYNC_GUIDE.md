# Upstream 同步指南（Sync Guide）

> 本 Fork（`yangxijia111/PowerToys`，分支 `cuin-dev`）与上游 `microsoft/PowerToys` 的同步流程、自定义区域清单与冲突应对。
> 配套阅读：`DEVELOPMENT_PLAN.md` 第 6/7 节。

## 1. 自定义区域地图（同步时的"我们改了什么"）

| 区域 | 文件 | 改动性质 | 冲突风险 |
|---|---|---|---|
| 品牌常量层 | `src/common/ManagedCommon/Branding.cs` | **纯新增** | 极低 |
| 中文资源层 | `src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw` | **纯新增** | 极低 |
| Presets 服务 | `src/settings-ui/Settings.UI/Services/Presets/*` | **纯新增** | 极低 |
| Presets 页面/VM | `src/settings-ui/Settings.UI/SettingsXAML/Views/PresetsPage.xaml(.cs)`、`ViewModels/Presets/*` | **纯新增** | 极低 |
| OOBE 场景页 | `SettingsXAML/OOBE/Views/OobePresetPicker.xaml(.cs)` | **纯新增** | 极低 |
| 覆盖检查脚本 | `tools/check_zh_cn_coverage.py` | **纯新增** | 无 |
| 设置导航分组 | `SettingsXAML/Views/ShellPage.xaml` | 重组 + 新增 2 项 | **高**（上游每加模块都改） |
| 页面路由 | `SettingsXAML/App.xaml.cs` | +1 case | 中 |
| 品牌标题/About | `Strings/en-us/Resources.resw`（4 个标题 key + 少量新增） | 修改+追加 | 中 |
| GeneralPage 署名 | `SettingsXAML/Views/GeneralPage.xaml(.cs)` | +1 卡片/2 属性 | 中 |
| OOBE 注册 | `OobeWindow.xaml(.cs)`、`OOBE/Enums/PowerToysModules.cs`、`OOBE/ViewModel/OobeShellViewModel.cs` | +1 项 | 中 |
| 安装器名称 | `installer/PowerToysSetupVNext/Product.wxs` | 2 处字符串 | 低 |
| 托盘品牌串 | `src/runner/tray_icon.cpp`（搜索 `[fork-brand]`） | 3 处字符串 | 低 |
| 图标资产 | `Assets/Settings/icon.ico`、`logo*.png`、`runner/svgs/icon.ico` | 二进制替换 | 低 |

> 原则：自定义尽量"新增文件"；修改上游文件时改动行数最少化，并打 `[fork-brand]` 注释锚点。

## 2. 标准同步流程

```bash
# 1) 取上游
git fetch upstream

# 2) 合并（推荐 merge：保留双方历史，冲突一次性处理）
git checkout cuin-dev
git merge upstream/main
#   若追求线性历史且改动小，也可: git rebase upstream/main

# 3) 冲突处理优先级
#    - ShellPage.xaml: 以上游为准保留其新增模块项，再把我们的分组结构套回去（见 §3）
#    - en-us Resources.resw: 接受上游全部新增 key，再叠加我们的品牌标题改动
#    - 其余纯新增文件: 不会冲突，确认仍被引用即可

# 4) 验证（全部通过才能继续）
python tools/check_zh_cn_coverage.py --fail-on-orphan   # 中文层孤儿检查
powershell tools/build/build-essentials.ps1             # 快速构建（x64 Debug）
dotnet 跑 Settings.UI.UnitTests                          # 或 msbuild /t:Test

# 5) 全量验证（发布前）
powershell tools/build/build.ps1 -Platform x64 -Configuration Release
powershell tools/build/build-installer.ps1

# 6) 提交合并结果
git commit   # merge commit
git push origin cuin-dev
```

## 3. ShellPage.xaml 冲突处理细则（最高频热点）

上游合并时该文件几乎必有冲突。处理顺序：

1. `git checkout --theirs` 不行——上游版本没有我们的分组；正确做法是手工合并：
2. 保留上游的**全部 NavigationViewItem 项**（x:Name/x:Uid/AutomationId 原样）；
3. 重新套用我们的**六组结构**：系统工具 / 窗口管理 / 输入与启动 / 屏幕与显示 / 文件管理 / 开发与高级；
4. 保留我们的两个新增项：`PresetsNavigationItem`（推荐配置）、`ScreenToolsNavigationItem`（屏幕与显示组头）；
5. 新上游模块默认放入"开发与高级"或按其性质归组，zh-CN 层补翻译；
6. 核对 `grep -c "NavigateTo=" ShellPage.xaml`（上游项数 + Presets 项）无丢失。

## 4. en-us Resources.resw 冲突处理细则

- 冲突几乎总是"上游追加了新 key"，直接接受上游块即可；
- 我们只改过 4 个标题 key（`SettingsWindow_Title`、`SettingsWindow_AdminTitle`、`OobeWindow_Title`、`OobeWindow_TitleTxt.Title`）与少量 `General_Fork*`/`Oobe_PresetPicker_*`/`Shell_TopLevelScreenTools` 新增 key；
- 合并后必须运行 `python tools/check_zh_cn_coverage.py`，孤儿清零后才提交。

## 5. 禁止事项（同步与日常都适用）

- 禁止全局字符串替换 / 批量重命名 namespace、GUID、COM ID、内部模块 ID；
- 禁止修改 `main` 分支；一切开发在 `cuin-dev`；
- 禁止为了过构建删除/跳过/弱化测试；
- 禁止动上游核心（runner/modules/common）功能逻辑——只允许 `[fork-brand]` 字符串级修改。

## 6. 版本与发布

- 版本号跟随上游（`src/Version.props`），不做本地版本分叉，便于用户对照上游 release；
- 发行版可辨识性由品牌层保证（安装器名、窗口标题、托盘 About、图标角标）。
