# M0-02 / M0-03 / M0-04：iOS 媒体验证入口

记录日期：2026-11-12（Asia/Shanghai）。源码基线：`f71ce77` 加本次未提交工作树；实现入口为 [Lyrava.App](../src/Lyrava.App/Lyrava.App.csproj)。本记录不证明 iPhone 或车机可用。

## 已实现的验证工具

- MAUI 10 的 C# 页面和 iOS 启动入口，后台 `audio` 声明；仅包含 iOS 目标。
- 由应用 DI 单例拥有的一个 `AVPlayer`，页面只发命令并显示状态。初始不播放；页面离开不销毁播放器，窗口销毁释放播放器和观察器。
- 内存中输入合法可访问的 HTTPS 音源；不保存媒体地址、不输出原生错误文本和签名 URL。输入框遮蔽内容并关闭文本预测，避免截图直接暴露地址。停止后移除媒体项，清空输入和系统播放信息，并释放音频会话。
- `AVAudioSession` playback 类别、锁屏标题/时长/进度、系统播放/暂停/停止/切换播放状态；目标车机的下一首/上一首在 M0 没有队列，明确禁用，留给 M1。
- 断开当前音频输出暂停，中断结束只有原先正在播放且系统允许恢复时才请求播放。用户在中断中暂停、断开或停止会清除恢复意图。
- 媒体准备最多 15 秒、seek 最多 5 秒，支持取消；后发命令使旧的准备/seek 回调失效。页面命令额外有 20 秒截止，显式暂停/停止会结束未完成命令；页面消失只退订状态通知，不取消后台准备。失败时释放媒体项、结束通知和音频会话，周期观察器看到无媒体项即停止处理。KVO、结束通知、周期时间观察器和遥控 target 均有释放路径。
- 一个有限真实音乐源旅程：输入最多两个公开 video ID 后立即清空输入框；应用单例顺序执行 C# `ResolveAsync` → `RefreshIfExpiredAsync` → 原生播放，仅收到当前媒体的真实 ended 通知后解析第二首。最多两首、总截止 30 分钟，源请求最多 10 秒（HttpClient 另有 12 秒截止）；任务由应用持有，不随页面隐藏终止。暂停、停止、路由断开或系统中断开始均取消后续旅程；中断活动期间拒绝新加载/恢复，当前曲目的中断恢复遵循原先播放意图和系统 `ShouldResume`，不会重建已取消的二首旅程。源站失败仅显示 `SourceFailure` 和 `SourcePlayability`，不会自动切换客户端、登录或部署服务。

页面分别显示共享层播放意图、`AVPlayer.TimeControlStatus` 和脱敏结果类别。`Playing` 意图只代表已发起播放；只有实机观测能够证明声音、后台行为与车机控制。

## 当前工具链和实际结果

| 项目 | 本次观察 | 结论 |
| --- | --- | --- |
| Windows SDK | `C:\Program Files\dotnet\dotnet.exe`，SDK `10.0.404` | 已固定于根 `global.json` |
| MAUI | 本机 pack/manifest `10.0.110`；Controls package `10.0.110` | 使用集中包版本；MIT，需实机裁剪/AOT验证 |
| iOS 引用/SDK | 实际 MSBuild 选择 `TargetPlatformVersion=26.5`；SDK 路径 `Microsoft.iOS.Sdk.net10.0_26.5/26.5.10315` | 仅 Windows 引用/构建观察，不是 Mac/Xcode 矩阵 |
| iOS 编译最低系统 | `SupportedOSPlatformVersion=15.0` | 只是编译值，不承诺 iOS 15 设备验收通过 |
| Mac 连接 | `IsMacEnabled=false` | 未提供 Mac/Xcode；不得推导原生 AOT 已完成 |
| restore | 下列命令，exit `0` | PASS，恢复 App 和 Core 依赖 |
| managed Compile | 下列 `-t:Compile`，exit `0`，0 warnings / 0 errors | PASS，仅 C# / iOS 绑定编译 |
| Windows Release build | 下列无签名 build，exit `0`，0 warnings / 0 errors | PASS，仅当前 Windows 可执行构建步骤；产物包含 DLL/复制的静态库，不等于可安装 IPA |
| format | 下列验证命令，exit `0` | PASS |
| archive / IPA | `publish -c Release --no-restore -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -p:BuildIpa=true`，exit `1` | FAIL：`Code signing must be enabled to create an Xcode archive.` 没有生成并验收签名 IPA |
| 音乐源→原生后台换曲 | 已实现最多两首的有限入口，包含 source ProjectReference 后 restore/Windows Release build exit `0` | 代码接线 PASS，真实源流/后台换曲仍 NOT_RUN；源站返回不可播时只停止并报告类别 |
| Release 裁剪/AOT、安装、手机、车机 | 没有 Mac/签名/设备记录 | NOT_RUN |

以上退出结果需要在最终工作树重新执行；未执行的设备步骤不能用编译 PASS 替代。根质量报告负责记录最终复核结果。

## 本机可复核命令

在仓库根运行，使用现有 .NET SDK。Windows 从 PowerShell 7 调用，不安装或修改全局工具链：

```powershell
dotnet restore src/Lyrava.App/Lyrava.App.csproj --disable-parallel -p:RuntimeIdentifier=ios-arm64 --verbosity minimal
dotnet build src/Lyrava.App/Lyrava.App.csproj -c Release --no-restore -p:RuntimeIdentifier=ios-arm64 -p:EnableCodeSigning=false -p:BuildIpa=false -p:UseSharedCompilation=false -t:Compile
dotnet build src/Lyrava.App/Lyrava.App.csproj -c Release --no-restore -p:RuntimeIdentifier=ios-arm64 -p:EnableCodeSigning=false -p:BuildIpa=false -p:UseSharedCompilation=false
dotnet format Lyrava.App.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet msbuild src/Lyrava.App/Lyrava.App.csproj -getProperty:TargetFramework,TargetPlatformVersion,MauiVersion,IsMacEnabled,EnableCodeSigning,RuntimeIdentifier
```

此处无签名 build 仅为了 Windows 代码检查。连接 Mac 后必须重新执行完整 Release 构建与 publish，并检查包、签名和设备运行结果。

## M0-03 仍缺的私有输入

| 输入 | 当前状态 | 验收记录方式 |
| --- | --- | --- |
| Mac / macOS / Xcode 与选中路径 | 未提供 | 记录型号、系统版本、`xcodebuild -version`、`xcode-select -p` 和 .NET/workload 版本 |
| iPhone 型号 / iOS 版本 | 未提供 | 公开记录型号和系统，精确 UDID 仅私有设备台账 |
| 目标车机型号 / 固件 / 支持命令 | 未提供 | 固定连接方式与车机命令矩阵 |
| Developer Program / App ID / Ad Hoc profile / distribution identity | 未提供 | 签名材料只留私有 Keychain/凭据位置；不把描述文件或密钥放入仓库 |
| 已知可播、合法可访问的固定音源 | 未固定 | 先证明原生媒体链路，再单独接入 M0-05；不把源站问题与原生播放器问题混记 |
| 手机及媒体网络路线 | 未提供 | 记录设备真实 Wi-Fi/蜂窝/代理条件，不能从 `HttpClient` 代理推导 AVPlayer 网络相同 |

Mac 上在维护者提供并批准的环境中，先确认 SDK/workload/Xcode 与当前 iOS SDK匹配，再运行下列命令。命令中的签名环境变量必须在私有环境设置；本次没有运行这些步骤。

```bash
dotnet restore src/Lyrava.App/Lyrava.App.csproj -p:RuntimeIdentifier=ios-arm64
dotnet build src/Lyrava.App/Lyrava.App.csproj -c Release -p:RuntimeIdentifier=ios-arm64 -p:EnableCodeSigning=true
dotnet publish src/Lyrava.App/Lyrava.App.csproj -c Release -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -p:BuildIpa=true -p:EnableCodeSigning=true -p:CodesignKey="$LYRAVA_CODESIGN_KEY" -p:CodesignProvision="$LYRAVA_CODESIGN_PROVISION"
```

签名变量不得放在命令回执、CI日志、截图或 issue 中。构建后记录 IPA 摘要、App ID/版本、签名/描述文件有效期；用受支持工具向登记设备安装，脱离调试器启动。普通 iOS AOT 与 NativeAOT 分别记录，不将其中一个状态套到另一个。

## M0-04 实机记录步骤

固定同一源码和已签名 Release IPA，以下每项记录时间、设备/系统/网络、期望/实际、PASS/FAIL、脱敏日志位置和未覆盖范围：

1. 冷启动保持静默，加载固定已知音源、播放/暂停/恢复/seek/停止，确认声音与页面/native状态一致。停止或取消后，确认没有遗留请求、进度回调和声音。
2. 锁屏与切换其他应用连续播放；按根 [质量门禁](quality-gates.md) 记录时长与跨曲目覆盖。用最多两条 ID 的有限入口，在第一首播放期间锁屏，确认第一首真实结束后才解析并播放第二首，并测试源站失败/过期刷新。跨20曲/2小时与真实自然到期覆盖仍待独立旅程；有限两首工具不等于 M1 持久化队列。
3. 锁屏播放/暂停、停止和切换播放状态；检查标题、时长、进度。下一首/上一首在工具中禁用，不能填为 PASS。
4. 连接固定蓝牙车机，逐一检查实际支持的媒体控制；停车或由乘员完成。断开/熄火至少 3 次均暂停，避免切回手机扬声器外放；重连不自动擅自播放。
5. 来电、Siri、导航播报等中断各至少 3 次；覆盖中断前播放、先前暂停、中断中主动暂停、中断中断开/停止，后两者结束后保持暂停/停止。
6. 媒体准备中快速暂停/停止、立即换音源、反复 seek、请求取消/超时；旧回调不能重新启动播放。无重复播放器/重复通知，窗口销毁后释放资源。
7. 单独记录 Wi-Fi/蜂窝变化、HTTP Range/seek、自然过期/受控失效和后台换曲。只有实际使用的源站会话策略被验证，才归入 M0-05；不能把已知静态音源 PASS 当作真实音乐源 PASS。
8. 源旅程每次最多两条 ID、30 分钟，记录超时自动停止；第一首播放/准备中主动暂停、停止、断开车机后，第二首不能被解析或启动。覆盖第一首/第二首正在解析时发生中断：中断结束前不能启动原生播放，已经取消的旅程不会自行恢复。准备时主动暂停、待媒体 Ready 后手动恢复，时长和seek入口应正确恢复。元数据 HttpClient 与系统 AVPlayer 的网络路径分别留证，App不假定元数据可访问即媒体可访问。

本工具提供验证入口，M0-03/04 的 `done` 仍依赖真实签名和设备证据，M0-06 在此之前不放行。

## 来源与依赖

App代码、SVG标识为本次原创；没有导入 Metrolist/InnerTubeX 实现，没有引入 Swift/Kotlin/JavaScript 或服务器。直接依赖 `Microsoft.Maui.Controls 10.0.110`（MIT）和项目 Core / Sources.YouTube；源站实现及缺口见 [音乐源可行性](m0-source-feasibility.md)。AVFoundation、MediaPlayer、UIKit 为系统平台绑定。真实 Release 包的裁剪/AOT与依赖许可清单仍需按固定 Mac 工具链核对。

参考：[MAUI 10 API](https://github.com/dotnet/maui/tree/10.0.110)、[MAUI Ad Hoc 分发](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/publish-ad-hoc?view=net-maui-10.0)。本机 `Microsoft.iOS.xml` / 引用程序集用于确认 C# 原生签名，并通过实际编译验证。
