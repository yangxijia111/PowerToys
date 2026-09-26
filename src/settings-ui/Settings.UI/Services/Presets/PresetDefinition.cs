// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// 场景推荐配置定义：一个场景对应一组建议启用的工具。
    /// 显示名称与描述不在此硬编码，通过 resw key（Preset_{Id}_Name / Preset_{Id}_Description）本地化。
    /// </summary>
    public sealed class PresetDefinition
    {
        public string Id { get; }

        /// <summary>应用该场景时建议启用的工具列表。</summary>
        public IReadOnlyList<ModuleType> EnableModules { get; }

        /// <summary>应用该场景时建议关闭的工具列表（可选，通常为空以保持用户现有配置）。</summary>
        public IReadOnlyList<ModuleType> DisableModules { get; }

        public PresetDefinition(string id, IEnumerable<ModuleType> enableModules, IEnumerable<ModuleType> disableModules = null)
        {
            Id = id;
            EnableModules = enableModules == null ? new List<ModuleType>() : new List<ModuleType>(enableModules);
            DisableModules = disableModules == null ? new List<ModuleType>() : new List<ModuleType>(disableModules);
        }
    }
}
