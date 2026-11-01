# 作业清单 · Windows 桌面悬浮窗

一个不依赖浏览器的 Windows 桌面小工具：开机后出现在屏幕右侧，帮你记住今天要做的作业，用高 / 中 / 低标出轻重缓急，做完并交掉后点“交付”，它就会从清单里离开。

- 原生 C# + WPF，`.NET Framework 4.8`，不安装运行时、不联网、不需要管理员权限
- 用 Windows 自带的 PowerShell 做宿主，兼容 Smart App Control 对未签名 EXE 的限制
- 默认是**紧凑焦点模式**：只显示当前最要紧的一项作业，窗口窄而短，不会铺满桌面
- 高度可以按住底边自由拖动，程序会记住你调好的高度；输入栏可以折叠
- 数据只保存在本机：`%APPDATA%\HomeworkDock\state.json`
- 支持置顶、托盘、开机自启、窗口位置记忆
- 已完成并交付的作业会从清单删除；删除操作支持撤销

---

## 安装

从 GitHub 下载时，先解压，再打开解压出来的 `homework-dock` 文件夹。

1. 打开 `homework-dock` 文件夹。
2. 双击 **`安装.cmd`**。
3. 看到“安装完成”后，桌面会出现“作业清单”快捷方式，程序会自动启动。
4. 如果第一次启动稍慢（约 1 秒），是因为 Windows 正在启动隐藏的 PowerShell 宿主；之后会快一些。

> **为什么用 PowerShell 启动？** 部分 Windows 电脑（尤其是新装的 Windows 11）会启用 Smart App Control，它可能拦截没有数字签名的新 EXE。程序把界面编译成 DLL，交给 Windows 自带的、已签名的 PowerShell 加载；快捷方式会隐藏 PowerShell 窗口，你看到的仍然只有桌面悬浮窗。程序**不会关闭或修改 Smart App Control**，也不需要管理员权限；脚本和数据都只在本机，不联网。

安装位置：

```text
%LOCALAPPDATA%\Programs\HomeworkDock
```

安装器会创建：

- 桌面快捷方式
- 开始菜单快捷方式（含“卸载作业清单”）
- 开机启动快捷方式
- Windows“应用和功能”里的卸载项

不需要管理员权限。安装不会覆盖或删除已有的作业数据。

### 系统要求

- Windows 10 / Windows 11（64 位）
- .NET Framework 4.8（Windows 10 1903 以后和 Windows 11 通常已自带）
- 如果提示缺少 .NET Framework，可从微软官网安装：<https://dotnet.microsoft.com/download/dotnet-framework/net48>

---

## 第一次使用

启动后，窗口会出现在**桌面右侧**。它不是浏览器页面，也不会随浏览器关闭。

默认进入**焦点模式**：只显示当前最紧急、截止最近的一项作业；其余作业点卡片下方的 **展开全部** 进入详细列表，点筛选栏右侧的 **回到焦点** 再收起来。

### 添加作业

输入栏默认折叠成一条 **“＋ 添加一项作业”**，点它展开：

1. 写下作业，按 **Enter** 或点右侧的 **+**。
2. 需要收起时，点输入栏右侧的向上箭头，界面会回到简洁状态。

- 默认优先级是“中优先”
- 添加后会自动回到“全部”筛选，并高亮新作业
- 在焦点模式下添加时，窗口会临时加高一点，收起输入栏后恢复原来的高度

### 标记轻重缓急

- 输入框下方的 **高 / 中 / 低**：决定新作业的初始优先级
- 每张作业卡上的优先级标签：点一下就会循环切换  
  `高优先 → 中优先 → 低优先 → 高优先`
- 列表默认按“未完成优先、优先级从高到低、截止日期从近到远”排序

### 设置截止日期

点输入框右侧的 **截止日期**：

- 快捷选择：今天 / 明天 / 后天 / 下周一
- 也可以直接在日历里选日期
- 已过期的日期会显示红色“逾期 N 天”，今天截止会显示琥珀色“今天截止”

### 做完、交付、删除

1. 点作业左侧的圆圈，表示**做完了**。
2. 作业会进入“待交付”状态，并显示绿色 **交付** 按钮。
3. 点 **交付**：确认已经交掉，这条作业会从清单中**删除**，同时“今日已交付”加 1。

如果只是不想做了，可以点作业右侧的垃圾桶直接删除；删除后底部会出现“撤销”，大约 7 秒内可以恢复。

### 筛选和进度

详细列表里可以筛选：

- **全部**：所有作业
- **高优先**：只看高优先级
- **待交付**：只看已勾选完成、还没交付的作业
- 底部显示“已做完 X / Y”和今日交付数量

### 窗口操作

- **拖动顶部空白区域**可以移动窗口；位置会自动记住
- **拖动窗口底边中央的小横条**可以调整高度；高度会自动记住
- 右上角四个按钮：置顶 / 设置 / **最小化到托盘** / **退出作业清单**
- **置顶**打开时，窗口会浮在其他窗口上方
- **最小化到托盘**后，双击任务栏通知区域的图标可以重新显示；也可以再双击桌面快捷方式唤回
- **退出作业清单**会真正关闭程序（不是最小化）；下次开机或双击快捷方式再启动
- 托盘右键菜单：显示/隐藏、开机自启、打开数据文件夹、退出

**设置**里可以切换“紧凑焦点”、恢复默认高度、开关置顶和开机自启、把窗口移回屏幕右侧，或打开数据文件夹。“最小化”和“退出”不再放在设置里，直接在右上角。

### 快捷键

| 快捷键 | 作用 |
| --- | --- |
| `Ctrl + N` | 把光标放到输入框 |
| `Ctrl + ,` | 打开设置 |
| `Ctrl + W` | 隐藏到托盘 |
| `Esc` | 关闭弹窗 / 编辑框 |

---

## 开机自启

默认开启。启动时会自动在下面这个文件夹创建快捷方式：

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\作业清单.lnk
```

可以在两个地方开关：

- 窗口右上角 **设置** → **开机自启**
- 托盘右键菜单 → **开机自启**

如果系统策略阻止创建启动快捷方式，可以手动把桌面的“作业清单”快捷方式复制到上面的 Startup 文件夹，再在快捷方式属性的“目标”一栏末尾追加 ` --startup`。

---

## 数据与备份

所有作业都保存在：

```text
%APPDATA%\HomeworkDock\state.json
```

- 这是普通 JSON 文件，可以直接复制备份
- 修改作业后自动保存，不需要手动点保存
- 如果文件损坏，程序会把它备份成 `state.corrupt-日期-时间.json`，然后从空清单开始
- 程序不联网，不会上传任何内容

换电脑时，把 `state.json` 复制到新电脑的同一位置即可。

---

## 卸载

任选一种：

1. Windows **设置 → 应用 → 已安装的应用 → 作业清单 → 卸载**
2. 开始菜单 → 作业清单 → **卸载作业清单**
3. 在项目文件夹里双击 `卸载.cmd`

卸载时会询问是否删除作业数据：

- 直接回车：**保留** `%APPDATA%\HomeworkDock`
- 输入 `y`：同时删除作业数据

卸载程序只清理自己创建的快捷方式、启动项和注册表项，不会碰其他文件。

---

## 常见问题

### 双击后没有反应 / 窗口不见了

- 看任务栏通知区域（可能在小箭头里）有没有作业清单图标，双击它
- 如果之前点过“隐藏到托盘”，窗口只是藏起来了，不是退出
- 检查任务管理器里有没有 `powershell.exe` 进程（程序以隐藏的 PowerShell 宿主运行）

### 开机没有自动出现

- 打开窗口右上角设置，确认“开机自启”是开的
- 检查 `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` 里有没有“作业清单”快捷方式
- 某些安全软件会拦截启动项，需要手动允许

### 提示“保存失败”或数据没有保存

- 确认当前用户对 `%APPDATA%\HomeworkDock` 有写权限
- 程序会自动尝试 `%LOCALAPPDATA%\HomeworkDock`、`%TEMP%\HomeworkDock` 等备用位置；如果弹出“数据目录”提示，按提示路径查找
- 详细错误会写入 `%TEMP%\HomeworkDock-crash.log`

### 窗口被其他窗口挡住

- 点右上角的图钉打开“置顶”
- 全屏游戏/视频可能强制覆盖其他窗口，退出全屏即可看到

### 窗口高度不合适

- 按住窗口底边中央的小横条上下拖动即可；松手后自动记住
- 设置 → **恢复默认高度** 可以回到紧凑模式的默认高度
- 进入详细列表时窗口会自动展开；点“回到焦点”会恢复到之前的紧凑高度
- 程序跟随 Windows 的显示缩放（DPI），字体太小可以在 Windows“设置 → 系统 → 显示 → 缩放”里调整

### 升级后旧作业还在吗

在。升级只替换程序文件，`%APPDATA%\HomeworkDock\state.json` 不会被删除；第一次打开新版本会自动使用紧凑焦点模式，可以随时在设置里切回详细列表。

---

## 从源码构建

项目不依赖 Visual Studio、.NET SDK、Node 或 Electron，使用 Windows 自带的 .NET Framework 编译器。

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
& ".\build.ps1"
```

输出：

```text
dist\HomeworkDock.dll
dist\run.ps1
dist\app.ico
```

程序编译成 DLL，由 `run.ps1` 交给 Windows PowerShell 运行；这样即使 Smart App Control 开启，也不会因为“未签名 EXE”被拦截。

开发时可以用测试数据目录启动，避免影响正式数据：

```powershell
$env:HOMEWORKDOCK_DATA_DIR = "E:\path\to\test-data"
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\dist\run.ps1 --no-autostart
```

- `--no-autostart`：本次启动不创建/修正开机启动项，仅用于测试
- 设置 `HOMEWORKDOCK_DATA_DIR` 可以指定数据目录

### 主要文件

```text
src\Program.cs          启动、单实例、托盘、异常处理
src\MainWindow.cs       悬浮窗界面与交互
src\Data.cs             作业模型、JSON 持久化
src\Theme.cs            配色与控件样式
src\TrayIcon.cs         托盘图标与菜单
src\StartupManager.cs   开机启动快捷方式
src\AppHost.cs          PowerShell 宿主/图标路径
src\run.ps1             PowerShell 启动器
src\AssemblyInfo.cs     程序集信息
src\app.manifest        DPI / 权限清单
build.ps1               编译脚本
install.ps1             安装脚本
uninstall.ps1           卸载脚本
安装.cmd / 卸载.cmd     双击入口
assets\app.ico          程序图标
dist\                   预编译的 DLL、run.ps1、app.ico
.gitignore              本机数据、日志、测试截图、压缩包的忽略规则
.gitattributes          PowerShell 脚本 BOM 与换行规则
LICENSE                 MIT 许可证
```

---

## 版本

1.1.0 · 2026-11

这一版新增了紧凑焦点模式、可折叠输入栏和底部拖拽调高，并把“最小化 / 退出”移到了右上角。

## 许可证

本项目以 **MIT License** 发布，详见 [LICENSE](LICENSE)。你可以自由使用、修改和再发布，但请保留版权与许可证声明。

## 发布建议

- 仓库里保留了 `dist` 预编译版本，下载后可以直接双击 `安装.cmd`；如果只想维护源码，可以把 `dist/` 加进 `.gitignore`，让使用者先运行 `build.ps1`。
- 建议把 `作业清单-1.1.0-Windows.zip` 作为 GitHub Release 的附件，而不是直接提交进仓库。
- 公开仓库的 README 已经改成通用表述；如果你要使用自己的名字或 GitHub ID，可把 `LICENSE` 里的 `HomeworkDock contributors` 替换掉。

作业清单是一个本地小工具，没有账号、没有云同步、没有广告。
