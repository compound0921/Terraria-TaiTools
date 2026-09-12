# 时装法袍与宝石法杖

1.4.5.8 的 Player.PackGemStaffFeatures 在发射时读取正式胸甲，套装奖励钩子无法覆盖它。本补丁在本机游戏副本内修改 14 处法袍判定，将正式栏（包括原版有效装备查询）与时装胸甲栏的宝石法袍效果取并集。相同效果不重复叠加，保留法杖自身效果与同宝石加成的原版规则。

涵盖紫晶追踪、黄玉爆炸、蓝玉双发、翡翠速度变化、红玉散射、钻石碰撞范围、琥珀反弹。伤害倍率等后续计算继续使用原版代码。补丁不添加运行时 DLL 依赖，不修改服务端。

## 准备（游戏可保持运行）

使用 TerrariaPatcher 配套的 Mono.Cecil.dll，传入实际路径：

```powershell
.\scripts\prepare-robe-fix.ps1 -TerrariaPath 'D:\SteamLibrary\steamapps\common\Terraria' -CecilPath 'D:\Tools\TerrariaPatcher\Mono.Cecil.dll'
```

只生成 build/robe-fix/Terraria.exe 与输入/输出哈希，绝不覆盖运行中的游戏。补丁拒绝不匹配的 IL 和重复安装；游戏版本变化后必须重新准备并验证。构建目录中的游戏 exe 不得提交或上传。

## 安装（用户自行退出后）

```powershell
.\scripts\install-robe-fix.ps1 -TerrariaPath 'D:\SteamLibrary\steamapps\common\Terraria'
```

安装器发现 Terraria 运行就停止，不关闭也不启动游戏。确认原文件与准备时一致后备份并替换 exe；备份为 Terraria.exe.before-taitools-robes-时间.bak。随后由用户自行启动。

## 验证范围

TestRobeFix.cs 直接调用补丁副本的方法：8 种正式胸甲 × 8 种时装胸甲 × 7 种弹幕 × 2 种手持物，共 896 组通过，包含无宝石法袍、同种/不同种法袍组合和 AllGems 分支。此测试不启动游戏或载入存档。尚待用户在游戏中测试发射与联机表现。

可用 .NET Framework x86 csc 编译 TestRobeFix.cs，然后运行 TestRobeFix.exe <补丁exe绝对路径> <游戏目录>。
