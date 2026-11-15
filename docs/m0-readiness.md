# M0 证据与 M1 开工门禁

记录日期：2026-11-15（Asia/Shanghai）。源码身份：`main`，基线提交 `ee4da7fa0c3f69ed309cf1c09d109721e6bd349f` 加本次未提交工作树。证据适用于本次实际检查的树；后续修改必须重新执行相应检查。

M0 的工程和验证入口已实现，完整阶段尚未验收。Windows 首发切片已进入工程编译，Windows 运行、安装包和真实媒体门禁仍未完成；iPhone 的 M0-03/04 设备和签名门禁后置。不能把本地编译或 fixture 通过写成 M0 完成。

## 路线状态

| 路线 | 本次交付 | 尚缺的完成条件 |
| --- | --- | --- |
| M0-01 | 产品、架构、路线和协作基线已存在，随实施同步更新 | 无新的产品范围决定 |
| M0-02 | 固定 SDK、portable 工程、核心状态测试、Windows/iOS MAUI C# 页、验证脚本和 CI 配置 | 本地 portable 与 Windows Debug/Release build 已通过；托管 CI 矩阵尚未全绿（run `43676740983` 的 Ubuntu job 失败） |
| M0-03 | iOS 工程、构建命令、当前 Windows 工具链记录和私有输入清单 | 选定 Mac/Xcode/手机/车机/签名账号；已签名 Release IPA 脱离调试器安装运行 |
| M0-04 | 单例原生播放器、后台 audio 配置、系统命令、路由/中断处理、停止和资源释放入口 | 已知可播音源的真实 iPhone 锁屏、系统命令、蓝牙、来电和断开记录 |
| M0-05 | C# 搜索/解析/格式/过期/Range 探针、协议测试与有限旅程 | 真实可播放曲目、格式/Range/seek、自然过期/受控失效、必要会话和后台换曲 |
| M0-06 | Windows 首发的容量、依赖与未支持范围 | M0-05 实际通过；绑定 Windows 支持版本并完成继续/调整判断；iOS M0-03/04 后续单独处理 |

详细实验见 [Windows 验证](m0-windows-validation.md)、[iOS 验证](m0-ios-validation.md) 和 [音乐源可行性](m0-source-feasibility.md)。M0-05 的 2～4 人周验证窗口没有因为一次自动化开发会话而视为结束，也没有据此认定纯 C# 路线已经不可能。

## 验证层级

| 检查 | 状态与边界 |
| --- | --- |
| G0 文档 | 最终检查需覆盖相对链接、41 个路线 ID、许可、状态一致、UTF-8 和空白 |
| G1 portable | `eng/verify.ps1` 本地复核：restore/build/format/test 全部 exit 0，Core 11/11、Source 28/28，0 warnings/0 errors；托管 CI 矩阵未全绿 |
| iOS 托管编译 | Windows 上实际编译过 C# 与 iOS 绑定；本次复核不等于原生 AOT、链接、签名或可安装 IPA |
| G2 Windows 平台包 | 部分验证：Windows Debug/Release unpackaged build exit 0；`SystemMediaTransportControls` 接线已编译并完成一次 8 秒启动烟测；系统面板按键、MSIX、安装和升级仍 NOT_RUN |
| G2 iOS 完整平台包 | NOT_RUN；Windows build 返回 0 不代表 iOS 原生平台门禁 |
| archive / 签名尝试 | FAIL；当前环境没有启用归属本项目的签名条件，archive 报 `Code signing must be enabled to create an Xcode archive.` |
| G3 真实源 | 局部搜索有真实结果；完整播放旅程尚未通过，详见源探针记录 |
| G4/G5/G6/G7 Windows | NOT_RUN；缺少 Windows 真实播放、系统媒体控制、安装包和日常观察 |
| G4/G5/G6/G7 iPhone | NOT_RUN；缺少绑定的真实手机、车机、签名安装与日常观察 |
| G8 发行 | NOT_RUN；没有 Windows 安装包、IPA 或上传资产 |
| 托管 CI | FAIL（run `43676740983`，head `ee4da7fa0c3f69ed309cf1c09d109721e6bd349f`，2026-11-15）：Windows job PASS，Ubuntu job 在 `Restore, Release build, format and tests` 步骤 FAIL；不能用单个矩阵 job 或本地 PASS 覆盖该结果。详见 [run 43676740983](https://github.com/IoTSharp/Lyrava/actions/runs/43676740983)。 |
| NativeAOT publish | NOT_RUN；共享程序集的 AOT 分析器无警告不能替代实际发布 |

## 当前资源预算

这些是 M0 探针的实际代码边界，不是已经测量的手机耗电、内存、缓存或可用性指标。M1 扩容前必须重新审阅并在固定设备测量。

| 对象 | M0 边界 |
| --- | --- |
| Source HTTP | 单请求默认 10 秒，选项上限 30 秒；调用方持有 HttpClient、取消与网络路线 |
| 元数据响应 | 默认 2 MiB，选项上限 4 MiB；超限失败，JSON 深度最多 64 |
| 搜索解析 | 默认最多 20 条，选项上限 50；遍历/待处理节点最多 20,000、解析最多 2 秒 |
| 直接音频 | 仅当前可验证的 HTTPS GoogleVideo AAC-LC / MP4 直接地址；有效期保留 30 秒余量 |
| Range 探针 | 两个非相邻偏移，每次 16 字节；共用请求截止，206 与 Content-Range 必须一致 |
| CLI 旅程 | 一条选中结果，整体 25 秒，可取消；直连与显式代理分别记录，无自动重试 |
| iOS 已知音源 | 一个 AVPlayer；准备 15 秒，seek 5 秒，页面单次命令最多 20 秒 |
| iOS 源旅程 | 最多两条媒体标识；应用单例持有后续解析，整体最多 30 分钟；暂停/停止/路由断开取消 |
| 媒体缓存 / 预取 | M0 不持久化临时媒体地址、不落媒体缓存、不提前解析整张歌单；M1 的有限缓存预算待实测 |
| 工程检查 | 四个固定步骤；默认单步 120 秒、整体 600 秒，CI job 最多 15 分钟 |

网络证据必须区分 C# 元数据请求与 Windows `MediaPlayer`/iOS AVPlayer 媒体请求。Windows 的 `127.0.0.1:7890` 代理仅属于本次明确选择的本机路径；不能推导 iPhone 有同一路径，也不能证明源站在目标蜂窝网络可用。

## 依赖与来源

| 依赖 | 固定版本 / 许可 | 当前用途与兼容证据 |
| --- | --- | --- |
| .NET SDK / BCL | SDK 10.0.404；.NET 项目许可 | Core/Source/CLI 为 BCL 代码；版本精确固定；共享构建可检查 AOT 分析器，尚未执行 NativeAOT publish |
| Microsoft.Maui.Controls | 10.0.110；MIT | Windows/iOS C# UI；Windows 绑定编译不等于运行、MSIX 或裁剪/AOT通过 |
| Apple 平台绑定 | 实际 SDK 选择记录于 iOS 文档 | AVFoundation、MediaPlayer、UIKit；需对应 Mac/Xcode 和 Apple SDK 条件，不是新增 Swift/Kotlin 业务库 |
| Microsoft.NET.Test.Sdk | 17.14.1；MIT | 仅测试工具，许可已核对恢复后的 nuspec；不作为 iOS 运行时依赖 |
| xunit | 2.9.3；Apache-2.0 | 仅合同测试，许可已核对 nuspec |
| xunit.runner.visualstudio | 3.1.4；Apache-2.0 | 仅测试执行器，许可已核对 nuspec |

这是当前直接依赖清单。可分发 Windows 安装包和 IPA 的全部传递依赖、原生组件与许可对应仍属于实际平台包和 G8 检查；当前没有发行包，不能宣称已完成发行许可审计。

代码为新编写的 C#；未导入 Metrolist/InnerTubeX 实现，未引入 JavaScript 执行依赖、Kotlin/Swift 业务库或解析服务器。音乐源协议参考及其固定版本见源实验文档。没有把桌面子进程能力当作 iOS 功能。

## 尚未支持与解除条件

当前没有产品搜索页、完整队列、持久化收藏/歌单、下载、账号同步、完整 token/cipher 策略、Android/macOS 产品、CarPlay 或发行包。Windows 只有播放验证切片，普通蓝牙的验证入口不代表已经通过蓝牙兼容性验收。

解除 Windows 首发的 M0 阻塞需要：

1. 绑定受支持的 Windows 版本和实际网络条件，执行合法固定音源的真实播放、seek、过期刷新和窗口生命周期回执。
2. 完成 Windows Release、MSIX 或受支持安装包、安装升级、系统媒体控制和日常观察；unpackaged Debug build 不能替代这些证据。
3. 对仍失败的真实音乐源给出可复核原因和继续/调整决定。改变技术栈、增加服务器或 JavaScript/Kotlin/Swift 业务依赖必须另行决策。
4. 完成 M0-06，才在新的 Lyrava 监督会话以多个互不冲突的智能体开始 Windows M1。iPhone M0-03/04 在后续平台工作中单独解除，不作为 Windows 首发的隐含 PASS。

监督者会在最终 [HANDOFF](../HANDOFF.md) 记录真实结果、工作树、授权和任务资源。没有创建定时自动化、发行或部署服务。
