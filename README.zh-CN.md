# Lyrava · 莉拉瓦

[English](README.md)

使用 C#、.NET 和 MAUI 构建的开源音乐客户端，首个交付目标是在 iPhone 上实现稳定、可日常使用的蓝牙车载听歌体验。

**当前阶段：M0 工程与可行性验证。** 已建立 C# 共享播放状态核心、有界音乐源探针和最小 iOS MAUI 媒体验证客户端；尚无已签名 IPA 或应用发行版。以下首版功能仍是交付目标，真机、车机和完整真实音乐源门禁尚未通过。

## 第一版的实际目标

计划中的 v0.1 包含搜索与浏览、收藏和本地歌单、持久化播放队列、锁屏及后台连续播放、系统媒体控制、车载蓝牙输出、音频中断恢复和移动网络切换恢复，并通过 Ad Hoc 向登记的 iPhone 安装。

以“真实手机、真实车机、脱离调试器连续使用”的证据决定是否交付。搜索结果可见、前台播放一首歌或模拟器运行成功，均不能代替日常使用验收。

全部评估范围都会保留在路线图中：账号同步、离线下载、歌词、Android、Windows、macOS、高级音效、识曲、一起听、第三方集成，以及单独验证的 CarPlay 和平台小组件。

## 技术与分发约束

- 以 .NET 10 + MAUI 起步，界面、业务和平台适配代码使用 C#。
- iOS 通过 C# 调用 AVPlayer、AVAudioSession 和系统媒体控制接口；系统原生框架仍是平台依赖。
- 音乐源适配与播放器分离，优先验证 C# 音乐源、媒体格式、token 和后台换曲。
- 初始架构不要求独立服务器。引入解析服务器、Kotlin 二进制或 JavaScript 执行依赖前，必须明确变更语言和部署边界。
- 首期使用 Apple Ad Hoc 分发；仍需 Mac/Xcode、签名资产、设备 UDID 和有效描述文件。
- 普通蓝牙音频由系统管理；CarPlay 有独立授权与实现门槛。
- MAUI 10 的官方支持期限为 2027-05-11，路线图单独安排 SDK 升级，不能套用 .NET 本身的支持年限。

## 项目文档

| 文档 | 内容 |
| --- | --- |
| [ROADMAP](ROADMAP.md) | 全部阶段、交付 ID、依赖、状态与验收 |
| [功能覆盖表](docs/feature-coverage.md) | Metrolist 及本次评估功能对应到哪个阶段 |
| [架构](docs/architecture.md) | C# 共享层、平台层、音乐源、缓存与下载 |
| [验收门禁](docs/quality-gates.md) | 代码、构建、签名、真机、车机和日常使用的独立证据 |
| [M0 证据与开工门禁](docs/m0-readiness.md) | 实测结果、环境缺口、依赖清单和进入 M1 的条件 |
| [iOS 验证](docs/m0-ios-validation.md) | 媒体验证客户端、Mac 构建命令和手机/车机步骤 |
| [音乐源可行性](docs/m0-source-feasibility.md) | 纯 C# 匿名源探测与尚未成立的播放要求 |
| [技术决策](docs/adr/0001-platform-and-distribution.md) | 技术栈、全 C# 边界、分发、许可证 |
| [开发约定](AGENTS.md) | 自动化协作、验证和变更要求 |
| [交接](HANDOFF.md) | 当前事实与下一步 |

## 当前可用入口

`global.json` 固定实际 SDK。`Lyrava.slnx` 包含共享核心、音乐源、命令行探针和合同测试；`Lyrava.App.slnx` 单独构建 iOS MAUI 验证客户端，避免把共享核心构建结果当作平台验收。

使用 PowerShell 7+ 运行与 portable CI 相同的有界检查：

```powershell
pwsh -NoProfile -File ./eng/verify.ps1
```

脚本依次执行 restore、Release build、format verify 和测试，逐步设置超时并在失败时停止。准确命令及平台检查边界见[验收门禁](docs/quality-gates.md)。

音乐源探针只输出数量、格式和失败类别，不输出临时媒体地址。fixture、搜索成功和 HTTP Range 分别只能证明各自范围，不能代替完整源站、iOS seek、锁屏或车机证据。

开源许可为 [GPL-3.0-only](LICENSE)。M0 实现为新编写的 C# 代码，尚未导入上游实现。后续参考或复用 Metrolist / InnerTubeX 时遵守[来源说明](NOTICE)；当前依赖及其 AOT 验证边界见 [M0 记录](docs/m0-readiness.md)。
