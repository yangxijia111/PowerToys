// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// 场景推荐配置服务：把场景定义批量应用到常规设置。
    /// 应用逻辑与持久化/IPC 分离，便于单元测试。
    /// </summary>
    public static class PresetsService
    {
        /// <summary>
        /// 把场景定义应用到常规设置配置对象（纯逻辑，不落盘、不广播）。
        /// 返回实际变更的工具数量。
        /// </summary>
        public static int Apply(PresetDefinition preset, GeneralSettings generalSettingsConfig)
        {
            ArgumentNullException.ThrowIfNull(preset);
            ArgumentNullException.ThrowIfNull(generalSettingsConfig);

            var changed = 0;
            changed += ApplyModuleList(preset.EnableModules, true, generalSettingsConfig);
            changed += ApplyModuleList(preset.DisableModules, false, generalSettingsConfig);
            return changed;
        }

        /// <summary>
        /// 应用场景并持久化：读当前配置 → 应用 → 保存 → 通过 IPC 通知 runner。
        /// onSendIpcMessage 由调用方注入（ShellPage.SendDefaultIPCMessage），便于解耦与测试。
        /// </summary>
        public static async Task<int> ApplyAndSaveAsync(PresetDefinition preset, Func<string, int> onSendIpcMessage)
        {
            ArgumentNullException.ThrowIfNull(preset);

            var repository = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default);
            var config = repository.SettingsConfig;
            var changed = Apply(preset, config);
            if (changed == 0)
            {
                return 0;
            }

            // 落盘后再广播，runner 收到消息即可刷新各模块进程
            SettingsUtils.Default.SaveSettings(config.ToJsonString());
            var outgoing = new OutGoingGeneralSettings(config);
            if (onSendIpcMessage != null)
            {
                await Task.Run(() => onSendIpcMessage(outgoing.ToString()));
            }

            return changed;
        }

        private static int ApplyModuleList(IReadOnlyList<ModuleType> modules, bool isEnabled, GeneralSettings config)
        {
            var changed = 0;
            foreach (var moduleType in modules)
            {
                if (ModuleHelper.GetIsModuleEnabled(config, moduleType) != isEnabled)
                {
                    ModuleHelper.SetIsModuleEnabled(config, moduleType, isEnabled);
                    changed++;
                }
            }

            return changed;
        }
    }
}
