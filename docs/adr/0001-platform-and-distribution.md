# ADR 0001：C# 客户端、平台媒体层与 Ad Hoc 分发

- 日期：2026-11-11
- 状态：已采纳架构方向；实现与设备可行性仍待 M0 验证。
- 范围：Lyrava 全部客户端及第一版交付。2026-11-15 起，第一版优先 Windows 桌面，iPhone 后置。

## 背景

维护者要求把 Metrolist 的音乐体验以 C# / .NET 10 技术栈重建到 iPhone，优先做到蓝牙车载日常听歌，并希望跨平台。iPhone 首期不通过 App Store，采用手动安装。完整范围必须保留在路线图中，不能因首期范围缩小而遗漏。

## 决策

1. 产品名为 **Lyrava（莉拉瓦）**，公开仓库为 `IoTSharp/Lyrava`。
2. 使用 .NET 10 + MAUI，自己维护的 UI、业务与平台适配代码用 C#。XAML 不作为必需条件，初始 UI 采用 C# 页面。
3. 共享队列与业务核心；使用 C# 调用各平台原生媒体能力。iOS 采用 AVFoundation/MediaPlayer。
4. YouTube/InnerTube 音乐源为独立适配边界。纯 C# token/签名和后台换曲是否成立由 M0 验证。
5. 初始架构没有必需的服务器；不默认引入 Kotlin 业务二进制或 JavaScript 执行依赖。需要改变时新增 ADR 并取得维护者对范围变化的决定。
6. v0.1 以 Windows 桌面为首个发布验收平台，使用 C# `Windows.Media.Playback.MediaPlayer`；iPhone、普通蓝牙车载、Android、macOS、高级功能和 CarPlay 分阶段实施。
7. Windows 首发先验证受支持安装包和升级路径；iPhone 后续仍采用 Apple Developer Program 的 Ad Hoc 分发，登记设备并维护签名。手动传输 IPA 不等于免签名安装。
8. 仓库新增作品采用 **GPL-3.0-only**，保持与计划中的 GPLv3 上游复用方向兼容。后续导入代码时逐文件审查其原许可并保留声明。
9. 版本维护单独规划。MAUI 10 官方支持到 2027-05-11，不能用 .NET 10 的支持周期代替 MAUI 的周期。

## 后果与未决项

- Windows 首发可以在 Windows 主机上完成托管编译和运行验收；当前 unpackaged build 不能替代 MSIX 或其他受支持安装包。
- 正常 iOS 构建和签名需要兼容 Mac/Xcode；Windows 可以承担共享核心和 UI 开发。
- AOT、裁剪、原生依赖和签名要在真实 Release 包验证。
- 注册设备数量、描述文件有效期和平台授权继续适用；不承诺任意 iPhone 安装。
- Google/YouTube 非公开接口变化需要持续维护；无法用一次移植获得永久兼容保证。
- 目标 iPhone、iOS 最低版本、车机、Mac、签名账号、可访问网络和源站会话策略尚未确定，属于 M0 的输入。
- 如 C# 音乐源验证失败，先给出可审阅的替代方案和影响，不悄悄降低语言、平台或后台播放要求。
- iOS 小组件、CarPlay 和其他扩展分别证明 C# 可行性与平台资格。

## M0 实施记录（2026-11-12）

已建立 .NET 10 共享工程、原创 C# 音乐源和 iOS MAUI 媒体验证入口。Windows 托管编译及合同测试只确认其各自范围；没有生成并验收已签名原生 IPA，也没有绑定手机或车机。

匿名源搜索具有局部真实网络证据，播放解析与完整后台旅程仍未通过；具体 profile、网络、退出结果和未测内容记录于 [音乐源可行性](../m0-source-feasibility.md)。这构成待验证风险，不构成已证明所有纯 C# 方案均不可行。

当前保留本 ADR 的架构方向；没有新增服务器、JavaScript 执行依赖或 Kotlin/Swift 业务库。Windows-only M0-06 的范围、容量边界和已知缺口见 [M0 记录](../m0-readiness.md)；M1-01 至 M1-04 可在该范围关闭后继续，真实源风险仍由 M1-05/OPS 处理。

## 首发平台调整（2026-11-15）

维护者将首发顺序调整为 Windows 优先，iPhone 后续实现。该调整不改变 .NET 10、MAUI、C#、共享核心、音乐源边界或不引入必需服务器/脚本运行时的约束；它只改变 M1 的首个平台和验收顺序。Windows 播放切片、构建边界和未完成的运行/安装门禁记录于 [Windows 验证](../m0-windows-validation.md)。iPhone 的 M0-03/M0-04 仍保留为后续平台门禁，不再作为 Windows 首发的隐含通过条件。

## M0-06 范围收口（2026-11-15）

维护者进一步明确：当前 M0 以 Windows 版本实现、工程基线和容量边界为完整范围。M0-06 在 Windows TFM、平台播放器/系统媒体控制接线、资源与取消边界、Release 构建、启动/清理烟测、依赖许可和未支持范围已记录后关闭，状态记为 `done（Windows-only）`。

该范围决定不会把真实源播放、系统媒体面板实际操作、MSIX/安装升级、长时间运行、日常使用或 Android/iOS 平台验收写成已经通过。真实音乐源继续作为 M1-05/OPS 风险，Windows 安装与日常使用作为 M1-07 门禁，Android/iOS/Mac/车机和签名作为后续平台阶段独立验收。若未来要把这些证据纳入 M0，需要新的维护者范围决定和相应 ADR 更新。

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
