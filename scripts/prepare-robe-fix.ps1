param(
    [Parameter(Mandatory=$true)][string]$TerrariaPath,
    [Parameter(Mandatory=$true)][string]$CecilPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'build\robe-fix'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$inputExe = Join-Path $TerrariaPath 'Terraria.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
Copy-Item -LiteralPath $CecilPath -Destination (Join-Path $outputDirectory 'Mono.Cecil.dll') -Force
$patcher = Join-Path $outputDirectory 'RobePatcher.exe'
& $compiler /nologo /target:exe /platform:x86 "/reference:$CecilPath" "/out:$patcher" (Join-Path $PSScriptRoot 'RobePatcher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Patcher compilation failed.' }
$beforeHash = (Get-FileHash -LiteralPath $inputExe -Algorithm SHA256).Hash
$outputExe = Join-Path $outputDirectory 'Terraria.exe'
& $patcher $inputExe $outputExe
if ($LASTEXITCODE -ne 0) { throw 'Patch preparation failed; do not install the staged executable.' }
if ((Get-FileHash -LiteralPath $inputExe -Algorithm SHA256).Hash -ne $beforeHash) { throw 'Input changed during preparation.' }
@{InputHash=$beforeHash; OutputHash=(Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash} |
    ConvertTo-Json | Out-File -LiteralPath (Join-Path $outputDirectory 'hashes.json') -Encoding utf8
Write-Host 'Prepared only. The running game and installed executable were not modified.'
