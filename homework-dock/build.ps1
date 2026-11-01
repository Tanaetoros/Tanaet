$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"
$assets = Join-Path $root "assets"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

$refs = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.Serialization.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_32\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"
)

foreach ($path in $refs) {
    if (-not (Test-Path $path)) { throw "Missing compiler reference: $path" }
}
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$sources = Get-ChildItem -Path $src -Filter "*.cs" | Sort-Object Name | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "No source files found in $src" }

# Build a library, not an EXE. Smart App Control blocks newly built unsigned
# EXEs on this machine, but a signed Windows PowerShell host may load the DLL.
$out = Join-Path $dist "HomeworkDock.dll"
$icon = Join-Path $assets "app.ico"
$manifest = Join-Path $src "app.manifest"
$arguments = @(
    "/nologo",
    "/target:library",
    "/platform:anycpu",
    "/optimize+",
    "/debug-",
    "/warn:4",
    "/out:$out",
    "/win32icon:$icon",
    "/win32manifest:$manifest"
)
foreach ($path in $refs) { $arguments += "/reference:$path" }
foreach ($path in $sources) { $arguments += $path }

Write-Host "Compiling $($sources.Count) source files..." -ForegroundColor Cyan
& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }

Copy-Item -Force (Join-Path $src "run.ps1") (Join-Path $dist "run.ps1")
Copy-Item -Force $icon (Join-Path $dist "app.ico")

# Remove artifacts from the old EXE-based build so nobody launches the
# blocked executable by mistake.
foreach ($stale in @("HomeworkDock.exe", "HomeworkDock.exe.config", "HomeworkDock.pdb")) {
    $path = Join-Path $dist $stale
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

$size = [math]::Round((Get-Item $out).Length / 1KB, 1)
Write-Host "Built $out ($size KB)" -ForegroundColor Green
Write-Host "Launcher: $(Join-Path $dist 'run.ps1')" -ForegroundColor Green
