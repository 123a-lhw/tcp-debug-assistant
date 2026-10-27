# TCP 调试助手（C# / WinForms）

一个单文件的 Windows TCP 调试工具，用来替代 NetAssist 那类"网络调试助手"：
既可以当**客户端**连服务端，也可以当**服务端**接受多个客户端，收发支持文本与十六进制、
可定时发送、可保存日志。

- 语言/框架：C# + .NET Framework 4.8 + WinForms（不用装 .NET SDK 或运行时补丁，Win10/11 自带）
- 程序本体：`TcpDebugAssistant.exe`（47 KB），双击即用，无第三方依赖
- **制作过程**：[docs/制作过程.md](docs/制作过程.md)——从环境约束、网络层设计、界面重做，到"看不到图片时怎么验证界面好看"

![界面截图：已连接状态](docs/images/ui-connected.png)

## 界面

- 深色顶部信息栏 + 浅灰页面 + 白色圆角卡片：左侧四张卡片（连接 / 接收显示 / 发送选项 / 客户端），
  右侧上方接收区、下方发送区，中间分隔条可拖动
- 输入框、按钮、复选框、客户端列表、客户端/服务端切换全部自绘扁平样式：
  主操作蓝色、断开/停止红色、次要操作描边；按钮有悬停与按下反馈，输入框获得焦点时边框变蓝
- 接收区日志分色显示：时间戳灰、方向标记与客户端编号青、收到内容深墨、发出内容蓝，一眼分清收发
- 字体统一 Microsoft YaHei UI，日志区用 Consolas 等宽字体；配色集中在 `src\App\Theme.cs`，改一处分色全界面生效
- 关键文字与背景的对比度都做过校验（最低 4.76:1，正文 14:1 以上），长时间盯日志不费眼

## 功能

**连接**
- TCP 客户端模式：填 `主机 + 端口` 连接，5 秒超时并给出明确错误
- TCP 服务端模式：监听本机端口，支持多个客户端同时接入，左侧列表实时显示
  `#1 127.0.0.1:53211` 这样的编号与地址（编号与接收区的 `← #1` 标记对应）
- 顶部信息栏实时显示：连接状态胶囊（圆点随状态变色）、累计收/发字节数、当前客户端数

**发送**
- 文本发送（编码可选 UTF-8 / GBK / ASCII / Latin1）或十六进制发送
- 十六进制支持 `01 0A FF`、`010AFF`、`0x01,0x02`、`01-02` 等写法，非法内容会在发送前提示
- 可追加换行（不追加 / `LF` / `CRLF`）
- 定时发送（最小 10 ms），失败时自动停止并说明原因
- 服务端模式下可"广播给所有客户端"，也可在客户端列表里按住 Ctrl 多选定向发送
- 发送历史（最近 20 条），点下拉框即可复用
- `Ctrl+Enter` 快速发送

**接收**
- 文本或十六进制显示可随时切换
- 可选时间戳 `[09:22:27.336]`，方向标记 `←`（收到）/`→`（发出）
- 多字节字符跨包拆分会正确拼接，中文不会乱码
- 控制字符显示为 `\xNN`，换行统一为 CRLF
- 自动滚动、暂停显示（暂停时仍在统计字节数）
- 一键清空、保存日志为 txt

**其它**
- 常用设置（模式/主机/端口/编码/各开关）自动保存到
  `%APPDATA%\TcpDebugAssistant\settings.ini`，下次启动沿用

## 快速开始

仓库里不含编译产物（`bin\` 已在 `.gitignore` 中），先编译一次：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
bin\TcpDebugAssistant.exe
```

自己跟自己试一遍（不需要别的程序）：

1. 选「TCP 服务端」，端口填 8000，点「开始监听」
2. 再开一个程序实例，选「TCP 客户端」，主机 127.0.0.1、端口 8000，点「连接」
3. 在客户端窗口点「发送」，服务端窗口接收区会看到 `[时间] ← #1 hello tcp`

## 目录结构

```
src\Core\            核心网络库（不依赖 WinForms，可单独复用）
  HexUtil.cs         十六进制解析/格式化
  TextCodec.cs       编解码、控制字符转义、跨包增量解码
  TcpEvents.cs       ClientInfo / 各事件参数 / 收发统计
  TcpServerSession.cs 服务端：监听、多客户端管理、定向发送与广播
  TcpClientSession.cs 客户端会话：连接、超时、收发、断开事件
src\App\             界面层
  Theme.cs            配色、字体、圆角绘制与对比度计算
  UiControls.cs       自绘控件：卡片、标题栏、扁平按钮/复选框/分段切换、状态胶囊、客户端列表
  MainForm.Layout.cs  控件创建与摆放（等价于设计器代码）
  MainForm.Logic.cs   连接/收发/显示/设置等交互逻辑
  SettingsStore.cs    %APPDATA% 下的简易 ini 读写
  Program.cs          入口，含全局异常兜底
  app.manifest        DPI 感知与 Windows 兼容性声明
tests\core\SelfTest.cs    核心库自检（含真实 socket 回环收发，18 项）
tests\gui\GuiHarness.cs   窗体验证：布局/配色/对比度 + 走真实按钮的交互流程
tests\gui-smoke.ps1       端到端冒烟：启动 exe、UI 自动化点击、截图
docs\制作过程.md          这个工具是怎么一步步做出来的（含踩坑与验证方法）
docs\images\              界面渲染图
build.ps1                 一键编译 + 测试
```

## 构建与测试

本机没有 .NET SDK 时，直接用 .NET Framework 自带的 C# 编译器（`csc.exe`）编译：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

产物在 `bin\`。加上 `-Run` 会在测试通过后直接启动程序：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Run
```

更完整的验证（会短暂弹出程序窗口、占用 8000 端口）：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -FullTest
```

`-FullTest` 会依次跑：

| 测试 | 覆盖内容 |
|------|----------|
| `SelfTest.exe` | 十六进制/编码/增量解码/统计 + 服务端接受连接·定向回发·广播·停止、客户端连接·收发·断开、连接被拒 |
| `GuiHarness.exe` | 主窗口能构造并显示、65 个控件齐全、无越界/裁切、卡片堆叠与分割条位置、配色对比度、渲染像素分布（深色标题栏/白色卡片/圆角） |
| `GuiHarness.exe --interactive` | 客户端模式：点「连接」→ 真实建立 TCP → 点「发送」→ 对端收到 9 字节 → 接收区出现回显 |
| `GuiHarness.exe --server` | 服务端模式：点「开始监听」→ 外部客户端接入进列表 → 广播到达 → 收到对端数据 → 停止后列表清空 |
| `tests\gui-smoke.ps1` | 独立 exe 的端到端：UI 自动化点击真实按钮 + 截图 `bin\gui-smoke.png` |

窗口渲染图会另存一份到 `bin\ui-render.png`，供自动化检查配色与圆角。

## 实现说明与限制

- 网络层用阻塞式 socket 读循环跑在后台线程，事件回传后由界面 `BeginInvoke` 切回 UI 线程；
  接收文本按 60 ms 批量刷到接收区，高频收包不会卡界面。
- 接收区超过 200 万字符会自动清空并提示，避免长时间抓包把内存吃满（重要数据请先保存日志）。
- 「GBK」用 .NET 的 `GB18030` 编解码实现，兼容 GBK/GB2312 的常见场景。
- 服务端监听 `0.0.0.0`（所有网卡）。若别的机器连不进来，检查 Windows 防火墙是否放行该端口。
- 源码按 C# 5 语法编写（本机只有 .NET Framework 自带的旧版编译器）。若你装了 VS 或 .NET SDK，
  可以直接把 `src\Core`、`src\App` 放进标准 csproj，语法都向前兼容。
