# BreakTick

Windows 上的轻量久坐提醒工具：常驻系统托盘，按工作节奏提醒休息。

## 当前功能

- 可配置工作时长、休息时长与每日目标。
- 右上、左上、中央和全屏休息提示。
- 休息期间检测到键鼠操作会暂停倒计时；连续点击三次才跳过。
- 本地 SQLite 会话记录、每日完成数、连续打卡、近 7 天统计和徽章进度。
- 程序重启后恢复计时；锁屏后解锁可重新开始工作计时。
- `Ctrl + Alt + B` 全局暂停/继续快捷键。
- 可设置工作时间，非工作时间自动暂停提醒。

## 使用

下载发布目录中的 `BreakTick.App.exe` 后直接运行。程序会驻留在 Windows 系统托盘；双击托盘图标可打开控制面板。

首次体验建议把工作时间设为 1 分钟、休息时间设为 20 秒。休息卡片出现后请停止键鼠操作，否则倒计时会暂停。

## 本地构建

项目使用 .NET SDK 8。为生成用户无需安装 .NET 的 x64 单文件包：

```powershell
.tools\dotnet\dotnet.exe publish src\BreakTick.App\BreakTick.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\BreakTick-win-x64
```

发布包位于 `artifacts\BreakTick-win-x64\BreakTick.App.exe`。
