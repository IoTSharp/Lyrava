# M0-06 · Windows 首发验证入口

记录日期：2026-11-15（Asia/Shanghai）。源码基线：`8976d56`。维护者已确定当前 M0 以 Windows 实现、工程基线和容量边界为完成范围；Android/iOS 及其签名、真机和车机验收保留为后续平台工作。本记录描述 Windows 垂直切片的工程入口，不把本地编译写成日常使用或发行通过。

## 当前实现

- `Lyrava.App` 同时目标 `net10.0-windows10.0.19041.0` 和 `net10.0-ios`，页面通过 `IMediaPlaybackService` 与平台播放器分离。
- Windows 平台使用 C# `Windows.Media.Playback.MediaPlayer`，单例服务持有媒体对象、播放状态、取消和资源释放；页面不持有长期播放器。
- Windows 服务已接入 `MediaPlayer.SystemMediaTransportControls`：启用播放/暂停/停止和时间线，接收系统按键与位置变更请求，更新脱敏标题、播放状态和可 seek 时间线。窗口销毁时退订事件并禁用系统媒体控制；系统级实际观察仍待运行回执。
- 直接媒体输入只接受 HTTPS URI，地址只在内存中使用，不写入状态、日志或诊断。源旅程最多两个公开 video ID，第二项在第一项结束后才解析。
- 当前 Windows 配置为 `WindowsPackageType=None`，用于先验证桌面代码和媒体路径。MSIX、签名、安装升级、自动启动和商店/分发均未实现。
- Windows 系统媒体控制的按键、进度条和窗口关闭后的进程行为，连同网络切换恢复和长时间播放，都需要独立运行回执；本地 build 不替代这些证据。

## Windows-only M0-06 收口

当前实现和工程证据已经满足 Windows-only M0-06 的范围：Windows TFM、`MediaPlayer`/`SystemMediaTransportControls` 接线、取消/超时和释放路径、Release build、启动/清理烟测、资源预算和未支持范围均已记录。M0-06 可以标记为 `done（Windows-only）`。

真实音源播放、系统媒体面板实际交互、网络恢复、MSIX/安装升级和日常使用继续记为 M1/OPS 的独立 `NOT_RUN` 或 `FAIL` 门禁。它们不会因为 M0-06 关闭而自动通过。

可重复执行的 Windows-only 验证入口为：

```powershell
pwsh -NoProfile -File ./eng/verify-windows.ps1 -StepTimeoutSeconds 300 -TotalTimeoutSeconds 900
```

脚本固定 SDK `10.0.404` 和 Windows TFM，执行 App restore、Release build、App solution format、8 秒 unpackaged 启动与本任务进程树清理；每次输出都会把真实源、系统媒体面板、MSIX/安装升级和长时间日常使用列为独立 `NOT_RUN` 边界。

## 已执行的工程验证

环境：Windows x64、PowerShell `7.6.10`、.NET SDK `10.0.404`、MAUI Windows workload `10.0.20`。

```powershell
dotnet restore Lyrava.App.slnx --disable-parallel --disable-build-servers
dotnet build src/Lyrava.App/Lyrava.App.csproj -f net10.0-windows10.0.19041.0 -c Debug --no-restore --disable-build-servers -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

本次 Windows Debug build：exit `0`，0 warnings，0 errors。iOS managed Debug build 也以相同共享接口回归通过；这两项只证明目标框架和托管代码可编译。

## 2026-11-15 Windows 控制面实现复核

在同一未提交工作树上重新执行：

```powershell
dotnet restore Lyrava.App.slnx --disable-parallel --disable-build-servers
dotnet build src/Lyrava.App/Lyrava.App.csproj -f net10.0-windows10.0.19041.0 -c Release --no-restore --disable-build-servers -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

两步均 exit `0`；Release Windows 目标为 0 warnings、0 errors。此次实现复核确认了 Windows 系统媒体控制的托管接线、元数据和时间线 API 可以编译；系统媒体面板实际按键、声音和窗口行为仍待运行回执，因此对应运行层仍为 `NOT_RUN`。

随后以同一 Release unpackaged 产物执行一次有界启动烟测：进程 `Lyrava.App.exe` 成功启动并保持存活 8 秒，随后只终止本次创建的 PID `35684` 及其子树，进程回读为 `PID_CLEAN`。该结果只证明桌面入口能启动且本次清理完成；没有加载音源、操作系统媒体面板或观察真实声音，系统媒体控制、播放、seek 和窗口日常行为仍为 `NOT_RUN`。

## Windows 运行回执模板

每次实际运行使用同一源码身份，公开记录只填脱敏摘要。不要记录授权头、cookie、有效媒体 URL、个人媒体记录或完整源站响应。

```text
记录 ID：WINDOWS-M0-YYYYMMDD-NN
记录时间（Asia/Shanghai）：
源码身份（commit 或工作树摘要）：
Windows 版本 / 架构：
SDK / MAUI workload：
构建类型：Debug/Release；unpackaged/MSIX

场景结果：
  - 冷启动保持静默：PASS/FAIL/NOT_RUN；摘要：
  - 合法固定 HTTPS 音源加载、播放、暂停、恢复、停止：PASS/FAIL/NOT_RUN；摘要：
  - seek 与时长显示：PASS/FAIL/NOT_RUN；摘要：
  - 无效地址、超时、取消和媒体失败分类：PASS/FAIL/NOT_RUN；摘要：
  - 两项源旅程只在第一项结束后解析第二项：PASS/FAIL/NOT_RUN；摘要：
  - Windows 系统媒体控制和窗口关闭资源释放：PASS/FAIL/NOT_RUN；摘要：
  - Wi-Fi/蜂窝或网络切换后的恢复边界：PASS/FAIL/NOT_RUN；摘要：
  - MSIX 打包、安装、卸载和升级保留数据：PASS/FAIL/NOT_RUN；摘要：

脱敏证据位置：
未覆盖范围与阻塞：
Windows 首发判定：PASS/FAIL/NOT_RUN/blocked
复核人：
```

`PASS` 只代表对应 Windows 版本、构建类型和场景实际观察到预期结果。unpackaged build 不能替代 MSIX 安装；fixture 或匿名搜索不能替代真实可播放源；Windows 通过也不能推导 iPhone、车机或后续平台通过。

系统媒体控制运行回执至少要覆盖：系统播放/暂停/停止按钮能驱动同一个播放器；播放状态、标题和进度时间线与页面一致；系统位置拖动触发有界 seek；窗口关闭后播放器、事件和进程按预期结束。没有这些观察时，代码接线只能记为平台构建层 `PASS`，运行层保持 `NOT_RUN`。

## 下一步

1. 进入 M1-01 至 M1-04，完成 Windows 搜索、队列、产品播放页、后台和系统媒体控制的产品化实现。
2. 在 M1-05 中用合法固定音源和可控网络条件执行真实播放、seek、过期刷新、网络恢复和源失败分类；不通过认证、年龄或区域限制。
3. 在 M1-07 中完成 Windows Release、MSIX 或受支持安装包、安装升级和日常观察；把每一项写入上面的模板。
4. Android、iOS、Mac、签名、真机、车机和平台专属能力继续按后续路线单独实施和验收。
