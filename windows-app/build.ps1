$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler not found.' }
$output = Join-Path $target 'PhoneGuard.exe'
& $compiler /nologo /target:winexe /optimize+ "/out:$output" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'PhoneGuard.cs')
if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
Write-Output $output
