param(
    [string]$TerrariaPath = 'G:\SteamLibrary\steamapps\common\Terraria'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'src\TaiTools.cs'
$outputDirectory = Join-Path $projectRoot 'build'
$output = Join-Path $outputDirectory 'TaiTools.dll'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$reLogic = Join-Path $env:LOCALAPPDATA 'TerrariaPatcher\ReLogic.dll'

$required = @(
    $compiler,
    $source,
    (Join-Path $TerrariaPath 'Terraria.exe'),
    (Join-Path $TerrariaPath 'PluginLoader.XNA.dll'),
    $reLogic
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "缺少编译依赖：$path" }
}

$xnaAssemblies = Get-ChildItem 'C:\Windows\Microsoft.NET\assembly\GAC_32' -Recurse -Filter 'Microsoft.Xna.Framework*.dll' |
    Select-Object -ExpandProperty FullName
if (-not $xnaAssemblies) { throw '没有找到 32 位 XNA Framework 程序集。' }

$sharedSources = Get-ChildItem (Join-Path $projectRoot 'shared') -Recurse -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$arguments = @('/nologo', '/target:library', '/platform:x86', "/out:$output")
$arguments += "/reference:$(Join-Path $TerrariaPath 'Terraria.exe')"
$arguments += "/reference:$(Join-Path $TerrariaPath 'PluginLoader.XNA.dll')"
$arguments += "/reference:$reLogic"
$arguments += $xnaAssemblies | ForEach-Object { "/reference:$_" }
$arguments += $source
$arguments += $sharedSources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码：$LASTEXITCODE" }
Write-Host "编译成功：$output"

