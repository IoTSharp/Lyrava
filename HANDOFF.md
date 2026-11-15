# Lyrava 交接

更新日期：2026-11-12（Asia/Shanghai）。分支：`main`。当前工作树包含本次 M0 未提交改动。

## 已确定与已完成

- 名称：Lyrava（莉拉瓦），由维护者选择。
- 目标：建立开源项目，完整规划全部评估范围，第一版交付可日常使用的 iPhone 车载核心版本。
- 技术方向：.NET 10 + MAUI；自有应用代码 C#；原生媒体层通过 C# 绑定调用。
- 分发方向：iPhone Ad Hoc；GPL-3.0-only 作为新增作品的许可证。
- 已编写 README、完整 ROADMAP、功能覆盖、架构、质量门禁、ADR、协作规则和贡献说明。
- M0-01 的文档基线已完成。GitHub 仓库的实际发布与提交状态以当前远端/Git 历史为准。
- M0-02 已建立 SDK `10.0.404`、portable `Lyrava.slnx`、共享播放意图核心与 11 项测试、M0 纯 C# 音乐源/CLI/28 项合同测试、iOS MAUI C# 媒体验证入口和有界本地/CI 检查脚本。
- 本次最终 portable 检查：restore、Release build、format、test 全部 exit 0；共享测试 11/11，音乐源测试 28/28，0 warnings/0 errors。iOS Windows managed Compile/Release build 与 format 也 exit 0；这些结果不包含原生签名、设备或车机证据。
- 真实匿名搜索在显式本机代理下得到结果；WEB_REMIX 选中结果解析返回 `Unavailable / Unplayable`。VisionOS profile 仅完成离线 bootstrap/请求合同测试，真实旅程暂未重跑。M0-05 仍未完成。

## 当前未完成

- 尚无已签名 Release IPA、原生 iOS AOT/裁剪、登记设备安装、iPhone 锁屏、目标车机或日常使用验收；Windows managed Compile 不替代这些门禁。
- Mac/Xcode、目标 iPhone/iOS、车机、签名账号、合法固定音源和设备媒体网络条件尚未绑定；Windows archive 复核以 `Code signing must be enabled to create an Xcode archive.` 失败。
- 纯 C# 音乐源已有代码、合同和局部搜索证据，但未取得可播放直接流；token/cipher、格式兼容、Range/seek、自然过期、锁屏后换曲仍未验证。
- 没有创建定时自动化，也没有部署解析或社交服务。

## 下一顺序

1. 收集并核对 `M0-03` 的 Mac/Xcode、目标 iPhone/iOS、车机、签名和合法固定音源输入；私有凭据不进入仓库或对话。
2. 在 Mac 上生成并独立核验 signed Release IPA，随后完成 `M0-04` 的锁屏、系统遥控、来电、网络、蓝牙断开/重连场景。
3. 继续 `M0-05` 的真实可播放曲目、格式、Range/seek、受控失效/自然过期、会话刷新和后台换曲；失败时更新 ADR 并由维护者决定边界变化。
4. 完成 `M0-06` 的设备支持矩阵、网络/缓存/超时预算和依赖许可记录。
5. 只有 `M0-06` 通过后，才在新的 Lyrava 监督会话中以多个不重叠智能体开始 M1-01 至 M1-07。

## 验证口径

规划文档检查与应用能力验收分开记录。G0 文档检查、G1 portable 和 Windows managed iOS 编译已分别取得本地结果；G2 完整平台包、G3 完整真实源、G4/G5/G6/G7/G8 仍为 NOT_RUN 或部分验证。不要从本仓存在、许可证、提交或 README 推导产品已经可用。

## 归属与授权

本次会话由维护者明确授权使用多个智能体完成 M0 工程与可行性实现；各智能体按不重叠目录工作，由主监督者整合。尚未提交、推送、创建 Release、部署或外部发布；未获得 Mac/签名/设备输入前不宣称 M0 完成，也不启动 M1 会话。后续智能体开始时必须重新读取 Git 状态及当前文档，不覆盖其他会话工作。

仓库中不保存本任务的临时工具文件、运行时截图、登录状态或签名资产。所有已返回的 Cordonix 任务进程均清理完成；只保留正常 `bin/obj` 构建输出和共享缓存，没有本任务需要长期保留的后台服务。VisionOS profile 的真实旅程尚未执行；远端托管 CI 查询为空，不能继承本地 PASS。
