// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ManagedCommon
{
    /// <summary>
    /// 本 Fork 的集中式品牌常量层。
    /// 规则：
    /// 1. 所有面向用户的"发行版名称"展示都应引用此处常量或对应 resw 资源 key，禁止散落硬编码。
    /// 2. 保留对上游 Microsoft PowerToys 的明确署名，不冒充官方版本。
    /// 3. 不修改 namespace、GUID、COM ID、内部模块 ID；不做事关代码结构的全局重命名。
    /// 4. C++ 侧（runner 托盘、安装器 Product.wxs）暂无法引用本类，
    ///    相关字符串需与本文件保持人工同步（搜索标记 [fork-brand] 定位全部位置）。
    /// </summary>
    public static class Branding
    {
        /// <summary>发行版显示名称（不含 "PowerToys" 前缀时使用）。</summary>
        public const string ForkName = "PowerToys Cuin";

        /// <summary>上游项目署名（About / 安装器说明使用）。</summary>
        public const string UpstreamName = "Microsoft PowerToys";

        /// <summary>上游项目地址。</summary>
        public const string UpstreamUrl = "https://github.com/microsoft/PowerToys";

        /// <summary>
        /// About/关于页的一行说明（含署名）。
        /// </summary>
        public const string AboutLine = "PowerToys Cuin — a community fork based on Microsoft PowerToys (MIT).";
    }
}
