# ADR 0001：C# 客户端、平台媒体层与 Ad Hoc 分发

- 日期：2026-11-11
- 状态：已采纳架构方向；实现与设备可行性仍待 M0 验证。
- 范围：Lyrava 全部客户端及第一版交付。

## 背景

维护者要求把 Metrolist 的音乐体验以 C# / .NET 10 技术栈重建到 iPhone，优先做到蓝牙车载日常听歌，并希望跨平台。iPhone 首期不通过 App Store，采用手动安装。完整范围必须保留在路线图中，不能因首期范围缩小而遗漏。

## 决策

1. 产品名为 **Lyrava（莉拉瓦）**，公开仓库为 `IoTSharp/Lyrava`。
2. 使用 .NET 10 + MAUI，自己维护的 UI、业务与平台适配代码用 C#。XAML 不作为必需条件，初始 UI 采用 C# 页面。
3. 共享队列与业务核心；使用 C# 调用各平台原生媒体能力。iOS 采用 AVFoundation/MediaPlayer。
4. YouTube/InnerTube 音乐源为独立适配边界。纯 C# token/签名和后台换曲是否成立由 M0 验证。
5. 初始架构没有必需的服务器；不默认引入 Kotlin 业务二进制或 JavaScript 执行依赖。需要改变时新增 ADR 并取得维护者对范围变化的决定。
6. v0.1 以 iPhone 为发布验收平台，普通蓝牙音频为核心；Android、桌面、高级功能和 CarPlay 分阶段实施。
7. iPhone 首期采用 Apple Developer Program 的 Ad Hoc 分发，登记设备并维护签名。手动传输 IPA 不等于免签名安装。
8. 仓库新增作品采用 **GPL-3.0-only**，保持与计划中的 GPLv3 上游复用方向兼容。后续导入代码时逐文件审查其原许可并保留声明。
9. 版本维护单独规划。MAUI 10 官方支持到 2027-05-11，不能用 .NET 10 的支持周期代替 MAUI 的周期。

## 后果与未决项

- 正常 iOS 构建和签名需要兼容 Mac/Xcode。Windows 可以承担共享核心和 UI 开发。
- AOT、裁剪、原生依赖和签名要在真实 Release 包验证。
- 注册设备数量、描述文件有效期和平台授权继续适用；不承诺任意 iPhone 安装。
- Google/YouTube 非公开接口变化需要持续维护；无法用一次移植获得永久兼容保证。
- 目标 iPhone、iOS 最低版本、车机、Mac、签名账号、可访问网络和源站会话策略尚未确定，属于 M0 的输入。
- 如 C# 音乐源验证失败，先给出可审阅的替代方案和影响，不悄悄降低语言、平台或后台播放要求。
- iOS 小组件、CarPlay 和其他扩展分别证明 C# 可行性与平台资格。

## 参考依据

- [MAUI 支持平台](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [MAUI C# UI FAQ](https://github.com/dotnet/maui/wiki/FAQs)
- [MAUI Ad Hoc 分发](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/publish-ad-hoc?view=net-maui-10.0)
- [MAUI 支持周期](https://dotnet.microsoft.com/en-us/platform/support/policy/maui)
- [Apple 设备登记](https://developer.apple.com/help/account/devices/devices-overview/)
- [Apple A2DP 路由](https://developer.apple.com/documentation/avfaudio/avaudiosession/categoryoptions-swift.struct/allowbluetootha2dp)
- [Apple CarPlay](https://developer.apple.com/carplay/)
- [Metrolist 参考代码](https://github.com/MetrolistGroup/Metrolist/tree/e6fea3b36c68841bad21daa6252e1d69ecd3afbe)
- [InnerTubeX 参考版本](https://github.com/MetrolistGroup/innertubex/tree/v0.11.2)
