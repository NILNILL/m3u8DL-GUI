# N_m3u8DL-CLI 图形界面版

> 本程序是开源项目 **N_m3u8DL-CLI** 的非官方图形界面版本，并非原作者发布的程序。
> 下载引擎及相关核心代码来自原作者 **nilaoda**，遵循 MIT 许可证。

一个适用于 Windows 的 m3u8 / MPD / 视频直链下载工具。粘贴下载链接后，程序可自动解析、下载分片、合并视频，并将结果保存到 `Videos` 文件夹。

## 使用

1. 双击 `N_m3u8DL-CLI.exe` 打开程序。
2. 在“下载链接”中粘贴视频地址。
3. 可选填写保存名称、线程、限速、代理和保存目录。
4. 点击“开始下载”。
5. 下载完成后，在 `Videos` 文件夹查看视频。

程序支持同时执行多个下载任务。任务列表显示名称、进度、速度、状态和地址；选中任务后，下方会显示速度、进度、总大小和状态。

### 重连选项

| 选项 | 说明 |
| --- | --- |
| 达到重连上限后继续下载 | 不受连续 5 次重连上限限制，下载失败后持续重试 |
| 重连成功后重置次数 | 开始重连后，只要下载进度继续增加，连续重连次数会重新从 0 计算 |

两个选项同时勾选时，持续下载优先。

### 主题

左上角可选择：

- 跟随系统
- 浅色模式
- 深色模式

主题选择会保存在当前用户配置目录中。

### 日志与删除

下载进度为每个任务固定一行，并直接更新原行；解析、重连、合并、警告和错误等信息会单独显示。

“删除选中”会在确认后停止对应任务，并清理该任务在保存目录中的最终视频、临时目录、已下载分片和未完成分片。

## 目录结构

```text
N_m3u8DL-CLI\
├── N_m3u8DL-CLI.exe
├── N_m3u8DL-CLI-engine.exe
├── ffmpeg.exe
├── README.md
├── Logs\
├── Videos\
└── bin\
    ├── build.ps1
    ├── src\
    │   └── N_m3u8DL-CLI.Gui.cs
    ├── Newtonsoft.Json.dll
    ├── CommandLine.dll
    ├── NiL.JS.dll
    ├── BrotliSharpLib.dll
    ├── MihaZupan.HttpToSocks5Proxy.dll
    ├── UACHelper.dll
    ├── System.Runtime.CompilerServices.Unsafe.dll
    ├── logo_3Iv_icon.ico
    └── NO_UPDATE
```

- 根目录存放程序、下载引擎和 ffmpeg。
- `bin` 存放脚本、GUI 源码和 DLL 运行库。
- `Logs` 存放下载与合并日志。
- `Videos` 存放下载和合并后的视频。

复制程序时请完整复制整个文件夹，不要只复制 EXE。

## 运行环境

- Windows 7 SP1 / 8.1 / 10 / 11
- `.NET Framework 4.0` 或更高版本
- 无需另外安装 ffmpeg

随附的 `ffmpeg.exe` 为 64 位版本，推荐在 64 位 Windows 上运行。

## 构建

在程序根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\bin\build.ps1
```

构建脚本会生成 `N_m3u8DL-CLI.new.exe`。确认程序运行正常后，用该文件替换根目录的 `N_m3u8DL-CLI.exe`。

## 命令行

无参数启动时打开图形界面；带参数启动时兼容原下载引擎命令行：

```bat
N_m3u8DL-CLI.exe "https://example.com/playlist.m3u8" --workDir "E:\video" --saveName "abc"
```

## 来源与致谢

| 项目 | 说明 |
| --- | --- |
| 原始项目 | N_m3u8DL-CLI |
| 原作者 | nilaoda |
| 原项目地址 | <https://github.com/nilaoda/N_m3u8DL-CLI> |
| 原项目文档 | <https://nilaoda.github.io/N_m3u8DL-CLI/> |
| 许可证 | MIT License |

下载、解析、解密和合并等核心功能来自原作者。再分发时请保留原作者版权声明和 MIT 许可证。

第三方组件包括 ffmpeg、Newtonsoft.Json、CommandLineParser、NiL.JS、BrotliSharpLib、HttpToSocks5Proxy 和 UACHelper，它们分别遵循各自的开源许可证。

## 免责声明

本程序仅供个人学习、技术研究及备份自己有权访问的内容。请勿下载或传播无权使用的内容，请勿用于商业或违法用途。
