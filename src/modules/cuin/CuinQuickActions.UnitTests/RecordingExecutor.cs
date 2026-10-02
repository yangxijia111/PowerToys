// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 记录型执行器 stub：捕获调用、可控返回值/抛异常。
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CuinQuickActions.Common.Actions;

namespace CuinQuickActions.UnitTests
{
    internal sealed class RecordingExecutor : IQuickActionExecutor
    {
        public List<QuickActionDefinition> Invocations { get; } = new();

        public bool NextResult { get; set; } = true;

        public Exception? ThrowOnExecute { get; set; }

        public Task<bool> ExecuteAsync(QuickActionDefinition action)
        {
            Invocations.Add(action);
            if (ThrowOnExecute is not null)
            {
                throw ThrowOnExecute;
            }

            return Task.FromResult(NextResult);
        }
    }
}
