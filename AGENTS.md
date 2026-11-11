# Lyrava 协作规则

## 产品与当前阶段

先读 README、ROADMAP、HANDOFF 和相关 docs。当前只有规划基线，第一版目标是 iPhone 车载日常听歌；计划中的功能不能写成已支持。

技术基线为 .NET 10 + MAUI，自有客户端代码使用 C#。音乐源、共享状态、存储和平台媒体层保持明确边界。不得未经维护者决定引入 Kotlin/Swift 业务库、JavaScript 执行依赖、必需服务器或更改核心技术栈。

## 工作归属与变更

- 开始先检查 Git 状态、当前分支、未提交改动和 HANDOFF。保留他人工作，不 reset/clean/stash-all 或宽泛覆盖。
- 一次变更围绕一个路线交付或独立缺口；在描述中引用路线 ID，不把计划 ID 冒称为 GitHub issue。
- 不因本文件自行派生子智能体；只有用户或明确任务规则要求时才委派，并分清文件归属和预算。
- 修改行为或阶段边界时同步 ROADMAP、相关文档、HANDOFF 和 CHANGELOG。
- Commit 使用 Conventional Commits；只暂存本次拥有的文件/修改。执行提交、推送、外部写入和发行遵循会话授权。

## .NET 与媒体实现

- 工程采用实际需要的 .slnx、src、tests 和版本配置；不创建无用途的空壳或空目录。
- Nullable、ImplicitUsings 和告警策略在 M0-02 固定；JSON 优先源生成；依赖说明许可证和 AOT/裁剪兼容性。
- 不压制关键裁剪/AOT 告警来制造兼容通过；不将普通 iOS AOT 等同于已完成 NativeAOT 验收。
- UI 页面不拥有长期播放器；网络、token、队列和媒体命令均有超时、取消和资源回收。
- 不在源码、日志、issue、截图或诊断包中保存 cookie、token、授权头、有效媒体签名 URL、Apple 私钥或设备 UDID。

## 验证与交付

遵守 docs/quality-gates.md。文档基线只做链接、ID、状态、许可和 Git 空白检查。当前没有应用工程，不能声称 build/test/手机/车机通过。

M0-02 建立工程时同步写出真实的 restore、Release build、format 和测试命令，并接入相同 CI。运行与改动相称的检查；mock、真实服务、AOT、签名、手机、车机、长稳和发行分别报告。

Windows 的 PowerShell 必须使用 7+；不得回退 Windows PowerShell 5.1。继承用户机器规则：有界循环/搜索/执行、工具从 PATH 或明确路径发现、按任务归属回收进程/临时文件。禁止全盘工具扫描，禁止按进程名批量终止共享进程。远程 shell 必须安全传递脚本。公共网络失败采用进程内或单次代理设置。

不使用 Graphify、其 MCP/服务或 graphify-out 生成流程。

## 交接

结束前记录实际完成、未完成、下一顺序、验证层级、授权边界、未提交改动和任务资源。不能因没有设备或签名资产而把门禁改成已通过；记录具体缺失输入。
