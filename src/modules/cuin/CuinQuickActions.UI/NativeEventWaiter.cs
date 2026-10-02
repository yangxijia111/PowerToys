// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 命名事件等待器（抄自 Peek.UI）：后台线程等待命名事件，
// 通过 DispatcherQueue 回调 UI 线程。事件驱动，无轮询。
using System;
using System.Threading;

using Microsoft.UI.Dispatching;

namespace CuinQuickActions.UI
{
    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            var t = new Thread(() =>
            {
                using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (eventHandle.WaitOne())
                    {
                        dispatcherQueue.TryEnqueue(() => callback());
                    }
                }
            });

            t.IsBackground = true;
            t.Start();
        }
    }
}
