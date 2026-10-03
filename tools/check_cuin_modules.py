# -*- coding: utf-8 -*-
# [fork-feature] Cuin 自有模块静态一致性检查（进 Cuin CI 与 Release Gate 的 Static checks）。
# 校验内容：
#   1. C++ 壳事件名（shared_constants.h）与 C# 侧（CuinConstants.cs）字面量一致
#   2. runner knownModules 注册了 Cuin 模块 DLL
#   3. EnabledModules / ModuleHelper / ModuleType / 序列化上下文 / Settings 导航的注册点齐全
#   4. 模块 UI resw 的 en-us 与 zh-CN 键集合一致
#   5. ActionCatalog 中每个动作的 Title/Description 键在两种语言 resw 中都存在
#   6. Settings 侧 resw 双语都含 CuinQuickActions 导航与页面键
import io
import os
import re
import sys

if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

failures = []


def read(path):
    with io.open(os.path.join(REPO_ROOT, path), "r", encoding="utf-8-sig") as handle:
        return handle.read()


def check(condition, message):
    if not condition:
        failures.append(message)


def resw_keys(path):
    text = read(path)
    return set(re.findall(r'<data name="([^"]+)"', text))


# 1) 事件名两侧一致 ---------------------------------------------------------
shared_constants = read(r"src/common/interop/shared_constants.h")
cs_constants = read(r"src/modules/cuin/CuinQuickActions.Common/CuinConstants.cs")

cpp_show = re.search(r'CUIN_QUICK_ACTIONS_SHOW_EVENT\[\] = L"([^"]+)"', shared_constants)
cpp_terminate = re.search(r'CUIN_QUICK_ACTIONS_TERMINATE_EVENT\[\] = L"([^"]+)"', shared_constants)
cs_show = re.search(r'ShowEventName = @"([^"]+)"', cs_constants)
cs_terminate = re.search(r'TerminateEventName = @"([^"]+)"', cs_constants)

check(cpp_show is not None, "shared_constants.h 缺少 CUIN_QUICK_ACTIONS_SHOW_EVENT 定义")
check(cpp_terminate is not None, "shared_constants.h 缺少 CUIN_QUICK_ACTIONS_TERMINATE_EVENT 定义")
check(cs_show is not None, "CuinConstants.cs 缺少 ShowEventName 定义")
check(cs_terminate is not None, "CuinConstants.cs 缺少 TerminateEventName 定义")


def cpp_unescape(literal):
    # C++ 宽字符串字面量里的 \\ 是转义的单个反斜杠；C# 的 @"" 为逐字字符串。
    return literal.replace("\\\\", "\\")


if cpp_show:
    cpp_show = cpp_unescape(cpp_show.group(1))
if cpp_terminate:
    cpp_terminate = cpp_unescape(cpp_terminate.group(1))
if cpp_show and cs_show:
    check(cpp_show == cs_show.group(1),
          "Show 事件名不一致: C++='%s' C#='%s'" % (cpp_show, cs_show.group(1)))
if cpp_terminate and cs_terminate:
    check(cpp_terminate == cs_terminate.group(1),
          "Terminate 事件名不一致: C++='%s' C#='%s'" % (cpp_terminate, cs_terminate.group(1)))

# 2) runner 注册 ------------------------------------------------------------
main_cpp = read(r"src/runner/main.cpp")
check("WinUI3Apps/PowerToys.CuinQuickActions.dll" in main_cpp,
      "runner main.cpp knownModules 缺少 PowerToys.CuinQuickActions.dll")

# 3) Settings 注册链 --------------------------------------------------------
check("CuinQuickActions," in read(r"src/common/ManagedCommon/ModuleType.cs"),
      "ModuleType.cs 缺少 CuinQuickActions 枚举成员")
enabled_modules = read(r"src/settings-ui/Settings.UI.Library/EnabledModules.cs")
check('[JsonPropertyName("CuinQuickActions")]' in enabled_modules,
      "EnabledModules.cs 缺少 CuinQuickActions 属性")
module_helper = read(r"src/settings-ui/Settings.UI.Library/Helpers/ModuleHelper.cs")
check("ModuleType.CuinQuickActions => generalSettingsConfig.Enabled.CuinQuickActions" in module_helper,
      "ModuleHelper.GetIsModuleEnabled 缺少 CuinQuickActions case")
check("case ModuleType.CuinQuickActions:" in module_helper,
      "ModuleHelper.SetIsModuleEnabled 缺少 CuinQuickActions case")
check("ModuleType.CuinQuickActions => CuinQuickActionsSettings.ModuleName" in module_helper,
      "ModuleHelper.GetModuleKey 缺少 CuinQuickActions case")
serialization = read(r"src/settings-ui/Settings.UI.Library/SettingsSerializationContext.cs")
check("[JsonSerializable(typeof(CuinQuickActionsSettings))]" in serialization,
      "SettingsSerializationContext 缺少 CuinQuickActionsSettings 注册")
check("[JsonSerializable(typeof(CuinQuickActionsProperties))]" in serialization,
      "SettingsSerializationContext 缺少 CuinQuickActionsProperties 注册")
check("[JsonSerializable(typeof(CuinQuickActionsSettings))]" in read(
    r"src/settings-ui/Settings.UI/SerializationContext/SourceGenerationContextContext.cs"),
    "SourceGenerationContextContext 缺少 CuinQuickActionsSettings 注册")
shell_page = read(r"src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml")
check("views:CuinQuickActionsPage" in shell_page,
      "ShellPage.xaml 缺少 CuinQuickActions 导航项")
check('case "CuinQuickActions": return typeof(CuinQuickActionsPage);' in read(
    r"src/settings-ui/Settings.UI/SettingsXAML/App.xaml.cs"),
    "App.xaml.cs GetPage 缺少 CuinQuickActions case")

# 4) 模块 UI resw 双语键集合一致 ---------------------------------------------
module_resw_en = resw_keys(r"src/modules/cuin/CuinQuickActions.UI/Strings/en-us/Resources.resw")
module_resw_zh = resw_keys(r"src/modules/cuin/CuinQuickActions.UI/Strings/zh-CN/Resources.resw")
only_en = module_resw_en - module_resw_zh
only_zh = module_resw_zh - module_resw_en
check(not only_en, "模块 zh-CN resw 缺少键: %s" % sorted(only_en))
check(not only_zh, "模块 en-us resw 缺少键: %s" % sorted(only_zh))

# 5) ActionCatalog 动作键全覆盖 ------------------------------------------------
catalog = read(r"src/modules/cuin/CuinQuickActions.Common/Actions/ActionCatalog.cs")
action_ids = re.findall(r'Id = "([a-z0-9_]+)"', catalog)
check(len(action_ids) >= 10, "ActionCatalog 动作数量异常: %d" % len(action_ids))
for action_id in action_ids:
    title_key = "QuickAction_%s_Title" % action_id
    desc_key = "QuickAction_%s_Description" % action_id
    check(title_key in module_resw_en, "en-us resw 缺少 %s" % title_key)
    check(desc_key in module_resw_en, "en-us resw 缺少 %s" % desc_key)
    check(title_key in module_resw_zh, "zh-CN resw 缺少 %s" % title_key)
    check(desc_key in module_resw_zh, "zh-CN resw 缺少 %s" % desc_key)

# 6) Settings 侧 resw 双语齐备 -------------------------------------------------
settings_resw_en = resw_keys(r"src/settings-ui/Settings.UI/Strings/en-us/Resources.resw")
settings_resw_zh = resw_keys(r"src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw")

# 7) solution membership（slnx 不 glob，必须显式注册）-----------------------------
slnx = read(r"PowerToys.slnx")
for project_path in [
    r"src/modules/cuin/CuinQuickActions.Common/CuinQuickActions.Common.csproj",
    r"src/modules/cuin/CuinQuickActions.UI/CuinQuickActions.UI.csproj",
    r"src/modules/cuin/CuinQuickActions.UnitTests/CuinQuickActions.UnitTests.csproj",
    r"src/modules/cuin/cuinquickactions/cuinquickactions.vcxproj",
]:
    check(('Path="%s"' % project_path).replace("/", "\\") in slnx or project_path in slnx,
          "PowerToys.slnx 缺少项目 %s（slnx 不自动收集，Release 全量构建会缺失）" % project_path)

# 8) installer payload 自动收集机制（WinUI3Apps glob + 排除表不含 Cuin 文件）-------
# 注意：Full Build 成功不代表 Installer 包含模块——这里锁定 glob 机制的关键 wiring。
installer_ps1 = read(r"installer/PowerToysSetupVNext/generateAllFileComponents.ps1")
for pattern in ['"*.exe"', '"*.dll"', '"*.pri"', '"*.json"', '"*.winmd"']:
    check(pattern in installer_ps1,
          "generateAllFileComponents.ps1 的 fileInclusionList 缺少 %s（模块文件不会被收进 MSI）" % pattern)
check("*.deps.json" in installer_ps1,
      "generateAllFileComponents.ps1 缺少 *.deps.json 收集（自包含 exe 的依赖清单会缺失）")
check("PowerToys.CuinQuickActions" not in installer_ps1,
      "generateAllFileComponents.ps1 不应按名排除 Cuin 模块文件")
winui3_wxs = read(r"installer/PowerToysSetupVNext/WinUI3Applications.wxs")
check("WinUI3ApplicationsFiles_Component_Def" in winui3_wxs,
      "WinUI3Applications.wxs 缺少生成占位符（glob 自动收集机制被破坏）")

# 9) CI 防回归：模块构建与单测步骤仍在 cuin-ci.yml --------------------------------
ci_yml = read(r".github/workflows/cuin-ci.yml")
check("cuinquickactions.vcxproj" in ci_yml, "cuin-ci.yml 缺少 C++ 壳构建步骤")
check("CuinQuickActions.UnitTests.csproj" in ci_yml, "cuin-ci.yml 缺少模块单测步骤")
check("check_cuin_modules.py" in ci_yml, "cuin-ci.yml 缺少 Cuin 模块静态检查步骤")

required_settings_keys = [
    "Shell_CuinQuickActions.Content",
    "CuinQuickActions.ModuleTitle",
    "CuinQuickActions.ModuleDescription",
    "CuinQuickActions_Enable.Header",
    "CuinQuickActions_Activation_GroupSettings.Header",
    "CuinQuickActions_BehaviorHeader.Header",
    "CuinQuickActions_CloseAfterLosingFocus.Header",
    "CuinQuickActions_SafetyHeader.Header",
    "CuinQuickActions_Safety_ConfirmNote.Header",
]
for key in required_settings_keys:
    check(key in settings_resw_en, "Settings en-us resw 缺少 %s" % key)
    check(key in settings_resw_zh, "Settings zh-CN resw 缺少 %s" % key)

if failures:
    print("check_cuin_modules: FAILED (%d)" % len(failures))
    for failure in failures:
        print("  - " + failure)
    sys.exit(1)

print("check_cuin_modules: OK (events, registrations, resw en/zh parity verified)")
