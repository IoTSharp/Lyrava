# 验收门禁

当前状态：Windows-only M0 已完成工程、合同测试、Windows 播放切片、容量边界和后置平台记录；M1-01/M1-02 已有首批 Windows 搜索/队列切片，M1-03/M1-04 正在推进。共享工程检查、Windows managed 编译与真实源的局部探测分别留证；Windows 运行/安装包、真实源和完整 iOS 原生构建、签名、手机、车机门禁继续作为 M1/OPS 或后续平台门禁。逐项结果见 [M0 记录](m0-readiness.md)。

## 证据格式

每份验收记录至少包含：路线交付 ID、源码提交或工作树身份、构建版本与配置、平台/系统/设备、测试步骤、期望与实际结果、PASS/FAIL/UNKNOWN/NOT_RUN、时间、日志或截图位置，以及未覆盖范围。公开记录只保留脱敏信息；签名资产、账户凭据、精确 UDID 和个人媒体记录留在私有环境。

| 门禁 | 验证内容 | 不能替代的内容 |
| --- | --- | --- |
| G0 · 文档 | 链接、路线 ID/依赖/覆盖、状态、许可证和变更一致 | 任何应用运行能力 |
| G1 · 共享核心 | 实际工程的 restore/build/format/测试，队列与状态合同 | 原生平台、真实音乐源 |
| G2 · 平台构建 | 目标平台 Release、裁剪/AOT、原生依赖与包结构 | 签名安装、运行、设备体验 |
| G3 · 真实音乐源 | 搜索、会话、解析、格式、seek、过期与刷新 | 后台音频和实际车机 |
| G4 · iPhone 真机 | 脱离调试器的后台、锁屏、中断和网络旅程 | 目标车机按键和日常长稳 |
| G5 · 目标车机 | 音频、支持的按键/元数据、断开/重连 | 其他型号车机 |
| G6 · 安装升级 | 登记设备安装、同 ID 升级、数据和到期信息 | 已长期稳定使用 |
| G7 · 日常使用 | 固定版本连续观察与问题闭环 | 未测平台、未测服务或公开发行 |
| G8 · 发行 | 源码、安装包、校验、说明、许可和远端回读一致 | 运行质量的自动证明 |

## 当前仓库检查

SDK 固定为 `10.0.404`。`Lyrava.slnx` 包含 Core、Source、SourceProbe 和两个合同测试工程；`Lyrava.App.slnx` 包含 Windows MAUI 播放切片和后置 iOS 探针。portable 检查不能覆盖平台工程。

Windows 使用 PowerShell 7+：

```powershell
pwsh -NoProfile -File ./eng/verify.ps1
```

脚本和 `.github/workflows/verify.yml` 运行以下同一组命令，每步默认 120 秒、整体默认 600 秒，失败即停止；脚本记录 PID、父 PID、启动时间、参数与退出码，取消/超时沿清理路径结束所属进程树。首次执行前检查脚本的参数边界；不要为通过检查而隐藏告警或跳过失败的测试。

```text
dotnet restore Lyrava.slnx --disable-parallel --disable-build-servers
dotnet build Lyrava.slnx -c Release --no-restore --disable-build-servers -m:1 -nodeReuse:false -p:UseSharedCompilation=false
dotnet format Lyrava.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet test Lyrava.slnx -c Release --no-build --no-restore --disable-build-servers -m:1 -nodeReuse:false --blame-hang-timeout 30s --logger "console;verbosity=normal"
```

本地脚本 PASS 与托管 CI PASS 分开记录。远端矩阵的每个 job 都必须成功；单个 job PASS 不能覆盖另一个 job FAIL。当前基线 run [43676740983](https://github.com/IoTSharp/Lyrava/actions/runs/43676740983) 已完成但为 `failure`：Windows job PASS，Ubuntu job 在共享验证步骤 FAIL；该结果与本地 PASS 分开记账，不能推导设备或真实源站通过。iOS 命令、当前 SDK/平台选择和真机操作步骤见 [iOS 验证](m0-ios-validation.md)；Windows 下 `Compile` 或跳过 Mac 目标的 build 返回 0，不代表已生成原生 Release IPA。

Windows 首发切片的目标框架和本次复核命令为：

```text
dotnet restore Lyrava.App.slnx --disable-parallel --disable-build-servers
dotnet build src/Lyrava.App/Lyrava.App.csproj -f net10.0-windows10.0.19041.0 -c Debug --no-restore --disable-build-servers -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

本次 Windows Debug build 为 exit `0`、0 warnings、0 errors。该结果只证明托管代码和目标框架可编译；当前 `WindowsPackageType=None`，MSIX、系统媒体控制、真实播放、窗口生命周期和日常使用仍是 NOT_RUN 或独立门禁。iOS managed build 的命令和设备步骤见 [iOS 验证](m0-ios-validation.md)。

Windows 服务已把 `MediaPlayer.SystemMediaTransportControls` 的播放/暂停/停止、元数据、时间线和位置请求接入单一播放器；后续系统面板按键、进度拖动和窗口关闭观察仍必须作为 G2/G7 运行证据单独记录。2026-11-15 重新执行 App restore 与 Windows Release build 均 exit `0`、0 warnings、0 errors，这只更新平台构建层证据。

Windows-only M0-06 的完成判定覆盖实现基线、构建入口、资源预算、依赖许可和已知限制；它不把真实源、系统面板、MSIX、安装升级或日常使用写成 PASS。上述证据进入 M1-05、M1-07 和 OPS 后再分别关闭。

Windows-only 验证脚本为 `pwsh -NoProfile -File ./eng/verify-windows.ps1 -StepTimeoutSeconds 300 -TotalTimeoutSeconds 900`；本次 SDK、restore、Release build、format、8 秒启动和所属进程树清理均 PASS，脚本明确输出真实源、系统媒体面板、MSIX/安装升级和长时间使用为 `NOT_RUN`。

文档检查包括相对链接、路线 ID/重复定义、状态、许可和变更一致性；使用 `git diff --check` 检查未暂存变更，暂存后再运行 `git diff --cached --check`。未跟踪的新文件也要检查 UTF-8、尾部空白和冲突标记，不能只依赖空的 staged diff。

## v0.1 的真实旅程

### 音乐源 G3

- 事先固定至少 20 首合法可访问的验证曲目，覆盖不同长度、歌单/专辑入口、可用音质和一个明确不可用条目。
- 搜索 → 详情 → 入队 → 播放 → seek → 切歌通过，错误分类符合实际。
- 至少一次真实过期或受控失效后的重新解析成功；受控失效与自然到期证据分别记录。
- 会话刷新、token 更新若被所选播放策略使用，必须在前台和锁屏连续换曲中验证。
- 明确真实请求使用的网络条件；不能把 fixture 响应视为源站 PASS。

### iPhone G4

- Release IPA 脱离调试器，锁屏连续播放至少 2 小时，至少跨 20 首曲目；记录是否覆盖真正的到期刷新。
- 切到导航等其他应用后播放持续；来电、Siri、音频打断与恢复每种适用场景至少验证 3 次。
- Wi-Fi/蜂窝切换、短时断网恢复、长时失败和手动取消各至少 3 次；无无限重试、重复播放或不可取消任务。
- 用户暂停后网络恢复不能自行恢复播放；停止后后台请求和预取能结束。
- 进程重建后恢复歌单/队列与进度，默认不擅自外放。
- 基本大字体、读屏和触控控件可用；复杂主题完整性留到 M5。

### 车机 G5

- 固定并记录车机型号/版本、连接方式和支持能力。
- 播放/暂停、上一首/下一首及歌名展示按目标车机支持能力逐项验证；不支持的封面或命令不得填 PASS。
- 熄火/断开至少 3 次均暂停，避免切回手机扬声器继续外放。
- 重连、来电和导航播报组合旅程按实际设备记录；系统不保证的自动唤起不列为已实现。
- 驾驶中需要人工操作的验证由乘员或在停车状态完成。

### 安装与日常 G6/G7

- 登记设备可以通过受支持安装工具安装 IPA；校验版本、App ID 与包摘要。
- 使用同 App ID 的新版包覆盖升级，收藏、歌单和队列保留；明确签名/描述文件到期日与重新部署步骤。
- 至少 7 天日常观察，每天累计不少于 30 分钟；严重崩溃、卡死、重复播放、外放泄漏和不可恢复的媒体问题清零后重新核对受影响场景。
- 记录设备型号、系统、网络、时长、问题和修复版本。先建立耗电/内存基线，不能凭感觉宣称低耗电。

## 后续阶段附加门禁

- M2：离线冷启动、下载中断、配额耗尽、清理与数据库迁移；歌词与 seek 同步。
- M3/M4：每个平台自己的构建、签名、安装、系统控制和后台证据；不继承 iOS 的 PASS。
- M5：基准音源、输出音频、时间轴、资源与功耗对照；音效关闭时基础播放不回退。
- M6：第三方授权/撤销、错误恢复、重复上报、一致性及用户数据边界。
- M7：平台资格/entitlement 与真实接收端验证分开确认。
- G8：发行安装包和源码版本对应；一次上传后独立回读，未知结果先核对再考虑重试。
