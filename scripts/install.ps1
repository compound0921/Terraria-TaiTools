param(
    [string]$TerrariaPath = 'G:\SteamLibrary\steamapps\common\Terraria'
)

$ErrorActionPreference = 'Stop'
$running = Get-Process Terraria -ErrorAction SilentlyContinue
if ($running) { throw 'Terraria 仍在运行。请先正常退出游戏再安装。' }

$projectRoot = Split-Path -Parent $PSScriptRoot
$plugins = Join-Path $TerrariaPath 'Plugins'
$sharedTarget = Join-Path $plugins 'Shared'
$source = Join-Path $projectRoot 'src\TaiTools.cs'
$target = Join-Path $plugins 'TaiTools.cs'

if (-not (Test-Path -LiteralPath (Join-Path $TerrariaPath 'PluginLoader.XNA.dll'))) {
    throw "目标目录没有 PluginLoader.XNA.dll：$TerrariaPath"
}
if (-not (Test-Path -LiteralPath $source)) { throw "缺少插件源码：$source" }

New-Item -ItemType Directory -Force -Path $plugins, $sharedTarget | Out-Null
if (Test-Path -LiteralPath $target) {
    $backupDirectory = Join-Path $plugins 'Backups'
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDirectory "TaiTools-$stamp.cs.bak") -Force
}

Copy-Item -LiteralPath $source -Destination $target -Force
Copy-Item -Path (Join-Path $projectRoot 'shared\*') -Destination $sharedTarget -Recurse -Force
Write-Host "安装完成：$target"
