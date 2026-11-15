# Lyrava 交接

更新日期：2026-11-15（Asia/Shanghai）。分支：`main`。Windows-only M0 基线提交：`c6d6206`；M1-01/M1-02 已在提交 `0e4e988` 完成首批切片，本次 M1-03/M1-04 状态合同和生命周期审计已完成并纳入后续提交。

## 已确定与已完成

- 名称：Lyrava（莉拉瓦），由维护者选择。
- 目标：建立开源项目，完整规划全部评估范围，第一版先交付可日常使用的 Windows 桌面核心版本；iPhone 和蓝牙车载版本后续实现。
- 技术方向：.NET 10 + MAUI；自有应用代码 C#；原生媒体层通过 C# 绑定调用。
- 分发方向：Windows 安装包优先，iPhone Ad Hoc 后置；GPL-3.0-only 作为新增作品的许可证。
- 已编写 README、完整 ROADMAP、功能覆盖、架构、质量门禁、ADR、协作规则和贡献说明。
- M0-01 的文档基线已完成。GitHub 仓库的实际发布与提交状态以当前远端/Git 历史为准。
- M0-02 已建立 SDK `10.0.404`、portable `Lyrava.slnx`、共享播放意图核心与 11 项测试、M0 纯 C# 音乐源/CLI/28 项合同测试、Windows/iOS MAUI C# 播放入口和有界本地/CI 检查脚本。
- 本次最终 portable 检查：restore、Release build、format、test 全部 exit 0；共享测试 11/11，音乐源测试 28/28，0 warnings/0 errors。Windows 上的 iOS managed Compile/Release build 与 format 也 exit 0；这些结果不包含原生签名、设备或车机证据。
- 本次 Windows 切片新增平台无关 `IMediaPlaybackService`、Windows `MediaPlayer` 服务和 Windows MAUI 启动入口；Windows `net10.0-windows10.0.19041.0` Debug/Release build 均 exit 0，0 warnings/0 errors。当前为 unpackaged build，运行、系统媒体控制、MSIX 和日常使用尚未验收。
- 本次继续实现把 Windows `MediaPlayer.SystemMediaTransportControls` 接入同一个服务：播放/暂停/停止、脱敏标题、状态/时间线更新和系统位置 seek 请求均有有界接线；重新执行 `Lyrava.App.slnx` restore 与 Windows Release build，均 exit 0、0 warnings/0 errors。尚未在系统媒体面板实际按键或加载音源，控制面板和真实播放运行层仍为 `NOT_RUN`。
- 使用同一 Release unpackaged 产物做了 8 秒启动烟测：`Lyrava.App.exe` PID `35684` 成功保持运行，随后仅清理本次创建的进程树，PID 回读为 `PID_CLEAN`。这只证明启动/清理边界，真实音频、系统媒体面板和日常运行仍 `NOT_RUN`。
- 维护者在本次新会话明确 Windows 版本实现即可完成当前 M0；Android、iOS、Mac、签名、真机、车机和平台专属媒体能力后置。M0-06 以 Windows-only 范围关闭，真实源、系统面板、MSIX、安装升级和日常使用继续作为 M1/OPS 门禁。
- M0-06 收口新增 `eng/verify-windows.ps1`：固定 SDK/Windows TFM，执行 App restore、Release build、format、8 秒 unpackaged 启动和所属进程树清理；本次脚本 exit 0，真实源、系统媒体面板、MSIX/安装升级和长时间使用明确输出 `NOT_RUN`。
- 本次后续实现新增 M1-01 Windows 搜索/浏览页、10 秒可取消搜索状态、M1-02 metadata-only `PlaybackQueue` 合同与 18 项 Core 测试、Windows 队列协调器和 Windows 根/队列页面；Windows Release build、App format、portable 验证和 iOS managed Compile 回归通过。
- 远端 run `43676740983` 已完成但整体失败：Windows job `132698426583` PASS，Ubuntu job `132698426746` 在共享验证步骤 FAIL；匿名日志端点返回 403，失败细节暂不能归因。该结果与本地 PASS 分开记账。
- 本次监督复核重新执行两次代理源旅程，均未取得可播放地址：`YouTube Audio Library --visionos` 返回 `Network/results=0`，`Creative Commons music` 返回 `results=0`；因此 M0-05 仍未完成，媒体/Range/seek/过期/后台换曲保持 NOT_RUN 或 FAIL。
- M0-03/M0-04 文档已补充脱敏验收回执模板；没有 Mac/Xcode、签名、iPhone、车机和合法固定音源输入，相关门禁仍为 blocked/NOT_RUN。

## 当前未完成

- 尚无 Windows Release/MSIX 或其他安装包、Windows 真实播放/系统媒体控制/日常使用验收；unpackaged Debug build 不替代这些门禁。
- 尚无已签名 Release IPA、原生 iOS AOT/裁剪、登记设备安装、iPhone 锁屏或目标车机验收；这些属于后续 iPhone 平台工作。
- Mac/Xcode、目标 iPhone/iOS、车机、签名账号、合法固定音源和设备媒体网络条件尚未绑定；Windows archive 复核以 `Code signing must be enabled to create an Xcode archive.` 失败。
- 纯 C# 音乐源已有代码、合同和局部搜索证据，但未取得可播放直接流；token/cipher、格式兼容、Range/seek、自然过期、锁屏后换曲仍未验证。
- 没有创建定时自动化，也没有部署解析或社交服务。

## 下一顺序

1. 完成 M1-01/M1-02 的真实 Windows 源、播放页状态、队列操作和系统媒体运行回执；私有凭据不进入仓库或对话。
2. 继续实现 M1-03 磁盘持久化、收藏/歌单 UI 和恢复回执，以及 M1-04 的真实系统媒体面板/长稳回执；本轮状态合同和生命周期审计已通过代码级验证。
3. 在 M1-05 继续 `M0-05` 的真实可播放曲目、格式、Range/seek、受控失效/自然过期、会话刷新和 Windows 网络恢复行为。
4. 在 M1-07 完成 Windows Release、MSIX 或受支持安装包、安装升级和日常观察证据。
5. Android、iOS、Mac、签名、真机、车机和平台专属媒体能力按后续阶段单独实施。

## 验证口径

规划文档检查与应用能力验收分开记录。Windows-only M0 的 G0/G1/Windows managed build 和启动烟测已有结果；托管 CI 矩阵未全绿（run `43676740983`），Windows 真实播放/安装包、G3 完整真实源、iPhone G4/G5/G6/G7/G8 仍为 NOT_RUN 或部分验证。不要从本仓存在、许可证、提交或 README 推导产品已经可用。

## 归属与授权

本次会话由维护者明确授权使用多个智能体完成 Windows M1 后续切片；各智能体按不重叠目录工作，由主监督者整合。文档收口本身不代表创建安装包、部署或外部发布；未获得 Mac/签名/设备输入前不宣称 iOS/Android 平台完成。后续智能体开始时必须重新读取 Git 状态及当前文档，不覆盖其他会话工作。

仓库中不保存本任务的临时工具文件、运行时截图、登录状态或签名资产。所有本次监督子任务均已完成并保持空闲；只保留正常 `bin/obj` 构建输出和共享缓存，没有本任务需要长期保留的后台服务。匿名 CI 日志端点因 403 未能提供 Ubuntu 失败细节；不得据此猜测原因或继承本地 PASS。

上一轮 M1-01/M1-02 文件已纳入 `0e4e988` 并推送到 `origin/main`。本次新增的状态合同、生命周期审计和路线文档改动必须在最终验证后一起提交；磁盘持久化、真实系统媒体面板和长稳运行仍未通过。
