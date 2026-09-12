# Terraria TaiTools

面向 Windows 版《泰拉瑞亚》1.4.5.8 与 TerrariaPatcher / PluginLoader 的原版增强插件。默认按 `U` 打开可拖动面板，也可以输入 `/menu`。

让 AI 安装请先阅读 [AI 安装指南](INSTALL_FOR_AI.md)。这是客户端项目，可以保持服务端不动；原版客户端需要先安装加载器，不能只复制本仓库就完成全部准备。

## 功能

- 连锁挖矿，可设置最大矿块数、距离限制与魔力消耗。
- 自动钓鱼：选择水面位置后自动抛竿、收杆，正常消耗鱼饵。
- 一键召来旅商、一键让旅商离开；保留 `/travel` 与 `/travel leave` 命令。
- Boss 召唤物不消耗，可在“工具”页开关，默认开启。
- 100 格 Buff 背包：药水、家具/环境增益、清酒、蜂蜜桶、花园侏儒和所有风筝均可放入；`Alt + 左键`控制单格生效或停用。
- 100 格旗帜背包：放入敌怪旗帜后持续获得对应旗帜攻防加成；同样支持单格停用。
- 所有世界难度固定增加 3 个独立饰品格，饰品使用原版 `ApplyEquipFunctional` 结算并按角色保存。
- 时装栏盔甲除了单件属性外，也会检查并启用完整套装奖励。
- 面板可拖动；快捷键可在“工具”页重新绑定。

## 安装

前提：目标 `Terraria.exe` 已由 TerrariaPatcher 加入 PluginLoader，游戏目录内存在 `PluginLoader.XNA.dll`。

1. 完全退出 Terraria。
2. 在 PowerShell 中进入本项目目录。
3. 执行：

```powershell
.\scripts\install.ps1
```

脚本默认安装到 `G:\SteamLibrary\steamapps\common\Terraria`。如果游戏在别处：

```powershell
.\scripts\install.ps1 -TerrariaPath 'D:\SteamLibrary\steamapps\common\Terraria'
```

插件源文件会安装到 `Terraria\Plugins\TaiTools.cs`，共享 UI 源码会安装到 `Terraria\Plugins\Shared`。旧版 `TaiTools.cs` 会先备份到 `Terraria\Plugins\Backups`。

## 离线编译检查

`build.ps1` 使用 Windows 自带的 32 位 .NET Framework C# 编译器，并引用游戏、PluginLoader、TerrariaPatcher 的 ReLogic 程序集以及 XNA GAC 程序集：

```powershell
.\scripts\build.ps1
```

成功后输出 `build\TaiTools.dll`。这个 DLL 主要用于提前发现 API/语法错误；实际安装仍使用 PluginLoader 的 `.cs` 源码加载方式。

## 操作

- `U`：打开/关闭面板（默认值，可在面板内改键）。
- `/menu`：打开/关闭面板。
- Buff/旗帜背包：普通左键存取；`Alt + 左键`切换该格生效状态。
- `/travel`：召来旅商。
- `/travel leave`：让旅商离开。
- `/autofish`：以鼠标世界坐标作为钓点开关自动钓鱼。

## 存档与限制

- Buff 背包、旗帜背包和额外饰品按角色名保存在游戏目录的 `Plugins.ini`。
- 自动钓鱼和旅商控制只在单人模式运行；Buff、旗帜与饰品效果以本地玩家为目标。
- 时装栏套装奖励已获用户确认生效。法袍赋予宝石法杖的特殊效果需要额外应用 [法袍补丁](ROBE_FIX.md)，仅更新 TaiTools.cs 不会启用它。补丁已通过 896 组实际方法离线测试，游戏内验证待完成。
- 尚未完成全面联机验证；服务器插件和原版同步机制可能影响效果，安装相同客户端插件不保证所有功能与单人一致。
- Boss 不消耗覆盖常规直接使用的召唤物；松露虫、向导巫毒娃娃等通过钓鱼或投掷触发的特殊媒介不改写原版消耗流程。
- 这是针对当前 1.4.5.8 可执行文件编译验证的插件；游戏更新后应重新运行离线编译并实机检查。

## 目录

```text
src/TaiTools.cs          插件主源码
shared/                  TerrariaPatcher 共享 UI 源码
scripts/build.ps1        离线编译检查
scripts/install.ps1      安装到游戏目录
build/                   编译输出（不提交）
```

第三方依赖来源见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
