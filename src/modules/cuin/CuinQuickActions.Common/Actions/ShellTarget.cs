// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 结构化的外部进程调用目标：固定 executable + 固定参数。
// 安全设计：目录里的所有 ShellTarget 都是编译期常量，不经过任何用户输入；
// 参数仅支持 %ENV% 形式的环境变量展开（ExpandArguments），杜绝 shell 拼接注入。
using System;

namespace CuinQuickActions.Common.Actions
{
    public sealed record ShellTarget
    {
        /// <summary>固定可执行文件（系统程序名或全路径）。</summary>
        public required string Executable { get; init; }

        /// <summary>固定参数（可为 null）；支持 %ENV% 展开。</summary>
        public string? Arguments { get; init; }

        /// <summary>展开参数中的环境变量（如 %SystemRoot%），无参数返回空字符串。</summary>
        public string ExpandArguments()
        {
            if (string.IsNullOrEmpty(Arguments))
            {
                return string.Empty;
            }

            return Environment.ExpandEnvironmentVariables(Arguments);
        }
    }
}
