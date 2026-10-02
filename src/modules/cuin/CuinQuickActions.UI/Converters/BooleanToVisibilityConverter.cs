// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 布尔到可见性转换器（WinUI3 无内置实现）。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CuinQuickActions.UI.Converters
{
    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            return value is true ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        {
            return value is Visibility.Visible;
        }
    }
}
