// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 面板用户设置读取接口。
namespace CuinQuickActions.UI.Services
{
    public interface IUserSettings
    {
        bool CloseAfterLosingFocus { get; }
    }
}
