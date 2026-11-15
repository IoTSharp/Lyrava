# M0-05 · C# 音乐源首次验证

日期：2026-11-15（Asia/Shanghai）。源码身份：`main` 的 `ee4da7fa0c3f69ed309cf1c09d109721e6bd349f` 加本次未提交工作树；运行环境为 Windows x64、PowerShell `7.6.0`、.NET SDK `10.0.404`、Release `net10.0`。Windows 已成为首发优先平台，iPhone 后置；本记录仍是源适配器的首次探针结果及监督复核，**M0-05 尚未通过，G3 为部分验证且播放链路阻塞**。

## 2026-11-15 监督复核

- 本地 `Lyrava.Sources.YouTube.Tests` Release 测试 28/28 通过，包含 fixture 形状漂移、取消、过期、Range、地址脱敏和 VisionOS bootstrap；这是离线合同证据，不是源站播放证据。
- GitHub Actions run `43676740983` 已完成但整体失败：Windows job `132698426583` 成功，Ubuntu job `132698426746` 失败。匿名 check-run 注释只提供 `Process completed with exit code 1`，日志端点返回 403，因此不能把失败步骤归因到源适配器；Actions 的 Node 20 弃用注释也不构成 M0-05 证据。
- 通过本机代理重新执行 `journey "YouTube Audio Library" --proxy --visionos`，搜索返回 `Network`、`results=0`，exit 2；重新执行 `journey "Creative Commons music" --proxy`，搜索完成但 `results=0`，随后 `resolve=NOT_RUN`、`range=NOT_RUN`，exit 2。两次均未取得可播放媒体地址，不能更新任何真实源、Range 或 seek 门禁。

## 已实现边界

- `Lyrava.Sources.YouTube` 是不引用 MAUI 的纯 C# 适配器，使用 BCL `HttpClient`、`System.Text.Json` 源生成请求和有深度/大小限制的 `JsonDocument` 响应读取。
- 匿名 `WEB_REMIX` 搜索和 player 请求；player 另提供显式选择的 `VISIONOS` profile。VISIONOS 仅在内存中 bootstrap 一次受控 visitorData，再发一次 player 请求；不自动切换 profile。没有 cookie、登录态、PO token、签名解密、JavaScript 执行、外部解析服务器或 Kotlin 库。
- 搜索只提取可识别的视频/音乐条目；不承诺完整专辑、歌手、歌单、分页或账号能力，这些仍归 M1/M2。
- 只选择带直接 HTTPS `*.googlevideo.com` 地址的 AAC-LC / MP4 音频；WebM/Opus、cipher 和未知传输不假称为 AVPlayer 可播。选择返回最高码率的候选，但原生解码兼容仍须真机验证。
- 临时媒体地址只在内存中交给播放器；`ToString()` 脱敏，地址属性禁止 JSON 序列化。CLI 不输出曲目、查询值、原始响应、有效地址、请求头或服务器自由文本错误原因，仅输出计数和枚举结果。
- 过期地址在播放前由 `RefreshIfExpiredAsync` 显式重新解析；没有常驻定时器或自动无限重试。Range 探针最多读取两个范围、合计32字节；它只能证明 HTTP 字节范围行为，不能证明原生 seek。

`YouTubeSourceOptions.ClientVersion` 当前固定为探针配置 `1.20241113.01.00`，属于本次原创请求配置，不是当前源站版本或完整协议兼容承诺。该配置在下述网络条件下只取得匿名搜索成功证据。尝试从官方匿名音乐首页有界读取配置版本未找到匹配值；未把猜测值当作实时发现。后续协议维护应重新取得并记录官方配置版本，再单独回归 player。

VISIONOS 的协议参考为 YoutubeExplode 固定提交 `6699da79956a45471b96c687b49a8c3f3b6da998`（MIT，仅核对机制，未复制源码）：先读取 `https://www.youtube.com/sw.js_data`，去除严格的 UTF-8 `)]}'` 后 LF/CRLF，再按固定数组路径提取 visitorData，随后使用 `VISIONOS` / `1.02` 与 Apple RealityDevice 请求 player。响应尺寸、JSON 深度、取消和总时限仍由本适配器限制；不接受自由文本前缀或数组形状漂移。visitorData 不可序列化到诊断或日志。

## 资源预算

| 项目 | 当前上限 |
| --- | --- |
| 单次源请求、Range 的两次请求合计 | 默认10秒，配置上限30秒，支持调用方取消 |
| CLI 整条旅程 | 25秒；Ctrl+C 取消；HTTP 连接5秒 |
| API 响应 | 默认2MiB，配置上限4MiB，JSON 深度64 |
| 搜索结果 | 默认20条，配置上限50条；遍历最多20000节点/2秒 |
| 格式选择 | 每个格式数组最多200项/2秒；临时地址最多16KiB |
| Range 探针 | `0-15` 与 `65536-65551`，要求精确206/Content-Range |
| 刷新/重试 | 每次显式调用最多一次解析；适配器不自动重试 |
| VisionOS profile | bootstrap 1 次 + player 1 次；不做 profile fallback；同一 CLI 旅程总截止25秒 |
| CLI journey 选择 | 只选一条结果，失败立即停止，不扫描其他条目绕过限制 |

`HttpClient` 由调用方持有；每个请求、响应、流、计时/取消源均有释放路径。CLI 分别记录直连与显式本机代理；API 的代理结果不证明 iOS 原生媒体网络路线相同。

## 实际结果

测试日期如上，未登录、无源站凭据。只读取公开服务，不产生远端写入。结果在进程输出中脱敏，以下为可公开复核摘要。

| 检查 | 实际结果 | 判定 |
| --- | --- | --- |
| Source + CLI Release build | exit 0，0告警/0错误 | PASS：仅托管工程构建 |
| fixture 合同测试 | 最终28项全部通过，exit 0（含形状漂移、取消、过期、Range、地址脱敏和 VisionOS bootstrap） | PASS：仅fixture |
| 匿名搜索，直连 | `Timeout`，exit 2 | FAIL：该网络路线未完成搜索 |
| 同一搜索，单次代理 fallback | `search=None; results=18`，exit 0 | PASS：仅真实匿名搜索 |
| 公开测试视频的 WEB_REMIX 解析，直连/代理 | 直连超时；代理 `Unavailable`，exit 2 | FAIL：未取得可播放流 |
| WEB_REMIX 真实搜索 journey，直连 | 搜索超时，exit 2 | FAIL：该网络路线 |
| WEB_REMIX 同一 journey，单次代理 fallback | `search=None; results=11; selected=1; resolve=Unavailable; playability=Unplayable`，exit 2 | FAIL：真实搜索成功，源站拒绝所选条目的播放解析 |
| VisionOS profile 真实 journey | 本次前缀修复后 **NOT_RUN**；修复前一次试验为 `ProtocolChanged`，不能继承为当前证据 | NOT_RUN：需单次重新选择的网络预算 |
| 真实媒体 Range / seek | 未取得可用媒体地址 | NOT_RUN |
| 真实过期刷新、token、后台换曲 | 尚无完整播放链路 | NOT_RUN |
| 至少20首矩阵、iPhone/锁屏/车机 | 未提供对应设备和签名基线 | NOT_RUN |

直连失败后只进行一次显式 `http://127.0.0.1:7890` fallback，没有盲目重试或变换客户端/身份来绕过源站限制。`Unplayable` 仅是允许公开的规范状态枚举；没有记录可能包含用户信息的源站错误原因。

## 复核命令

仓库根目录中运行，命令的外层执行器也应提供超时。默认 CI 只跑离线合同测试，真实源探针需要人工选择可合法访问的验证内容与网络条件。

```text
dotnet build tools/Lyrava.SourceProbe/Lyrava.SourceProbe.csproj -c Release --disable-build-servers
dotnet test tests/Lyrava.Sources.YouTube.Tests/Lyrava.Sources.YouTube.Tests.csproj -c Release --disable-build-servers --blame-hang-timeout 30s
dotnet tools/Lyrava.SourceProbe/bin/Release/net10.0/Lyrava.SourceProbe.dll search "Creative Commons music"
dotnet tools/Lyrava.SourceProbe/bin/Release/net10.0/Lyrava.SourceProbe.dll search "Creative Commons music" --proxy
dotnet tools/Lyrava.SourceProbe/bin/Release/net10.0/Lyrava.SourceProbe.dll journey "YouTube Audio Library"
dotnet tools/Lyrava.SourceProbe/bin/Release/net10.0/Lyrava.SourceProbe.dll journey "YouTube Audio Library" --proxy
dotnet tools/Lyrava.SourceProbe/bin/Release/net10.0/Lyrava.SourceProbe.dll journey "YouTube Audio Library" --proxy --visionos
```

`journey` 只探测一条搜索结果；查询中的来源名称不能作为曲目许可证证明。完整 G3 仍需事先固定至少20首合法可访问的曲目、不可用项和原生播放矩阵。CLI 退出码：`0` 表示请求范围完成，`2` 为明确源/协议/媒体失败，`3` 为取消或总截止，`64` 为参数错误。`0` 不代表 G3 整体通过。

## 继续条件与依赖

继续 M0-05 前需确定可实际播放的匿名/授权策略、token/签名依赖、网络路线与媒体格式；如果纯 C# 约束下不能成立，应提交 ADR 与影响评估由维护者决定。不得擅自加入脚本执行、业务库或服务器。VISIONOS profile 仅是受控可选实验，不能绕过 `LOGIN_REQUIRED`、付费或区域限制，也没有自动 Android/TV fallback。M0-03/04 的 Mac、签名资产、iPhone 和目标车机仍是后台换曲/原生 seek 的必要输入；当前不得启动依赖 M0-06 已通过的 M1 验收。

运行时零第三方 NuGet 依赖，仅使用 .NET 自带库（.NET 运行时相关代码按 MIT 许可发布）。新源码为原创 GPL-3.0-only，未导入或翻译 Metrolist/InnerTubeX 实现。测试使用仓库集中版本的 xUnit/Test SDK，许可与开发工具统一记录在工程依赖清单。`IsAotCompatible` 的分析器检查不等于 NativeAOT、iOS Release/AOT 或裁剪发布通过；这些均为 NOT_RUN。

本任务未创建长期服务。Cordonix 所有已返回执行收据均为 `cleanup_complete=true`、`active_processes_after_cleanup=0`。临时的首页版本提取源码已删除；正常 `bin/obj` 与 .NET 共享构建缓存未作宽泛清理。
