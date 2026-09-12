# 交给朋友 AI 的安装流程

目标：在朋友自己的 Windows Terraria 客户端安装 TaiTools，保持服务端不动。

## 1. 检查环境

克隆本仓库，读取 AGENTS.md 与 README.md。查找本机 Steam 库中的 `steamapps/common/Terraria`，必要时读取 Steam 的 `libraryfolders.vdf`；有多个安装目录时向用户确认。检查 `Terraria.exe` 的版本，当前插件针对 1.4.5.8 编译。不要把作者默认的 G: 盘路径当作朋友的安装路径。

确认 Terraria 已正常退出；备份原版 Terraria.exe、已有 Plugins 目录和 Plugins.ini 到带时间戳的独立目录。不要强制结束正在游玩的进程。

## 2. 准备加载器（原版客户端必做）

上游项目：https://github.com/dougbenham/TerrariaPatcher

作者环境使用的上游源码提交：`5af1ae7c2d524319170826fc09236a9d8400a616`。该提交目录包含打包的 TerrariaPatcher 工具。获取前检查来源和目标版本；不要盲目使用另一分支或更早版本，也不要用陌生来源的已修改 Terraria.exe 替换游戏。

使用 TerrariaPatcher 对朋友本机的 Terraria.exe 应用：

- PluginLoader（加载 TaiTools 所必需）
- Functional Social Slots（时装栏单件装备/饰品属性生效所必需）

作者还启用了扩大合成范围和取消每日一次渔夫任务限制。这些属于可执行文件补丁，TaiTools 源码本身不提供它们；要复制这些体验需要另外在 patcher 中配置，并实测联机是否符合预期。

确认游戏目录出现 `PluginLoader.XNA.dll`。仅检查这个文件存在不能证明 exe 已正确打补丁，需要后续启动确认加载器实际运行。传统工具使用 x86/.NET Framework，遇到 BadImageFormatException 时检查是否错误地用 64 位进程加载了 32 位程序集。

## 3. 安装 TaiTools

在仓库根目录执行，传入已经核实的实际路径：

```powershell
.\scripts\install.ps1 -TerrariaPath 'D:\SteamLibrary\steamapps\common\Terraria'
```

上面的 D: 路径只是示例。脚本会复制主源码和 shared 依赖；旧 TaiTools 源文件会保留为 `.cs.bak`。不覆盖 Plugins.ini。不要将其他旧版 TaiTools `.cs` 副本留在 Plugins 的子目录里，否则会重复定义。

可以运行 `scripts/build.ps1 -TerrariaPath ...` 做离线编译。它还需要 XNA 与 `%LOCALAPPDATA%\TerrariaPatcher\ReLogic.dll`；首次尚未完成加载器初始化时可能没有后一个文件，不要将依赖缺失误报为插件代码错误。

## 4. 验证

记录 PluginLoader.log 的现有长度/时间，启动游戏检查是否新增错误。进入测试角色或由用户选择的角色，按 U 检查菜单、分页、物品存取。再加入朋友的服务器检查实际功能，不用主菜单无错误代替联机验证。

当前限制：自动钓鱼与旅商控制明确只允许单人模式；法袍的追踪等特殊效果需要另行应用 [法袍补丁](ROBE_FIX.md)。部分战斗、物品、旗帜与额外饰品效果受客户端/服务端同步及服务端插件影响。不要承诺所有功能与单人完全一致。

## 5. 卸载与恢复

卸载前先在游戏中取出 Buff/旗帜背包和额外饰品格里的物品，正常退出并备份 Plugins.ini。随后移走 TaiTools.cs。Shared 可能被其他插件使用，不要直接删除。恢复原版 exe 前确认原始备份版本与当前游戏一致。
