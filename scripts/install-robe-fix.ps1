param([Parameter(Mandatory=$true)][string]$TerrariaPath)
$ErrorActionPreference = 'Stop'
if (Get-Process Terraria -ErrorAction SilentlyContinue) { throw '请自行正常退出 Terraria 后再安装。本脚本不会关闭游戏。' }
$staging = Join-Path (Split-Path -Parent $PSScriptRoot) 'build\robe-fix'
$hashes = Get-Content -LiteralPath (Join-Path $staging 'hashes.json') -Raw | ConvertFrom-Json
$target = Join-Path $TerrariaPath 'Terraria.exe'
$source = Join-Path $staging 'Terraria.exe'
if ((Get-FileHash -LiteralPath $target).Hash -ne $hashes.InputHash) { throw '游戏文件已变化，请重新准备补丁。' }
if ((Get-FileHash -LiteralPath $source).Hash -ne $hashes.OutputHash) { throw '暂存补丁校验失败。' }
$backup = $target + '.before-taitools-robes-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak'
Copy-Item -LiteralPath $target -Destination $backup
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $target).Hash -ne $hashes.OutputHash) { throw "安装校验失败，原文件备份：$backup" }
Write-Host "法袍补丁已安装。原文件备份：$backup"
Write-Host '请自行启动游戏测试。'
