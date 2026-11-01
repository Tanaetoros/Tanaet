param(
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$appName = '作业清单'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\HomeworkDock'
$sourceDll = Join-Path $PSScriptRoot 'dist\HomeworkDock.dll'
$sourceLauncher = Join-Path $PSScriptRoot 'dist\run.ps1'
$sourceIcon = Join-Path $PSScriptRoot 'dist\app.ico'
$sourceReadme = Join-Path $PSScriptRoot 'README.md'
$sourceUninstall = Join-Path $PSScriptRoot 'uninstall.ps1'
$psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Write-Step([string]$text) {
    Write-Host ('  ' + $text) -ForegroundColor Gray
}

function New-Shortcut([string]$path, [string]$target, [string]$arguments, [string]$description, [string]$icon, [string]$workDir) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $target
    if ($arguments) { $shortcut.Arguments = $arguments }
    if ($workDir) { $shortcut.WorkingDirectory = $workDir }
    $shortcut.Description = $description
    if ($icon) { $shortcut.IconLocation = $icon }
    $shortcut.Save()
}

function Stop-InstalledApp {
    Get-Process -Name 'HomeworkDock' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    $selfPid = $PID
    $parentPid = (Get-CimInstance Win32_Process -Filter ("ProcessId=" + $PID) -ErrorAction SilentlyContinue).ParentProcessId
    Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessId -ne $selfPid -and
            $_.ProcessId -ne $parentPid -and
            $_.CommandLine -and
            $_.CommandLine.IndexOf($installDir, [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
            $_.CommandLine.IndexOf('run.ps1', [StringComparison]::OrdinalIgnoreCase) -ge 0
        } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 400
}

try {
    Write-Host ''
    Write-Host ('  ' + $appName + ' · 安装程序') -ForegroundColor White
    Write-Host '  -------------------------' -ForegroundColor DarkGray
    Write-Host ''

    foreach ($path in @($sourceDll, $sourceLauncher, $sourceIcon)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw ('找不到程序文件：' + $path + '。请先运行 build.ps1。')
        }
    }

    $release = $null
    try {
        $release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop).Release
    } catch {
    }
    if (-not $release -or [int]$release -lt 528040) {
        Write-Host '  提示：没有检测到 .NET Framework 4.8，程序可能无法启动。' -ForegroundColor Yellow
        Write-Host '  可以从 https://dotnet.microsoft.com/download/dotnet-framework/net48 安装。' -ForegroundColor Yellow
        Write-Host ''
    }

    Write-Step '正在关闭旧版本…'
    Stop-InstalledApp

    Write-Step ('创建安装目录：' + $installDir)
    if (-not (Test-Path -LiteralPath $installDir)) {
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    }

    Write-Step '复制程序文件…'
    Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $installDir 'HomeworkDock.dll') -Force
    Copy-Item -LiteralPath $sourceLauncher -Destination (Join-Path $installDir 'run.ps1') -Force
    Copy-Item -LiteralPath $sourceIcon -Destination (Join-Path $installDir 'app.ico') -Force
    if (Test-Path -LiteralPath $sourceUninstall) {
        Copy-Item -LiteralPath $sourceUninstall -Destination (Join-Path $installDir 'uninstall.ps1') -Force
    }
    if (Test-Path -LiteralPath $sourceReadme) {
        Copy-Item -LiteralPath $sourceReadme -Destination (Join-Path $installDir '使用说明.md') -Force
    }

    # Remove the old, unsigned EXE and config. They are blocked by Smart App
    # Control on this machine and must not be launched by old shortcuts.
    foreach ($stale in @('HomeworkDock.exe', 'HomeworkDock.exe.config')) {
        $path = Join-Path $installDir $stale
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
    }

    $launcher = Join-Path $installDir 'run.ps1'
    $icon = Join-Path $installDir 'app.ico'
    $runArgs = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $launcher + '"'
    $startupArgs = $runArgs + ' --startup'

    Write-Step '创建桌面快捷方式…'
    $desktop = [Environment]::GetFolderPath('Desktop')
    New-Shortcut (Join-Path $desktop ($appName + '.lnk')) $psExe $runArgs ($appName + ' · 桌面悬浮窗') $icon $installDir

    Write-Step '创建开始菜单快捷方式…'
    $programs = [Environment]::GetFolderPath('Programs')
    $menuDir = Join-Path $programs $appName
    if (-not (Test-Path -LiteralPath $menuDir)) {
        New-Item -ItemType Directory -Path $menuDir -Force | Out-Null
    }
    New-Shortcut (Join-Path $menuDir ($appName + '.lnk')) $psExe $runArgs ($appName + ' · 桌面悬浮窗') $icon $installDir

    $uninstallInInstall = Join-Path $installDir 'uninstall.ps1'
    if (Test-Path -LiteralPath $uninstallInInstall) {
        $psArgs = '-NoProfile -ExecutionPolicy Bypass -File "' + $uninstallInInstall + '"'
        New-Shortcut (Join-Path $menuDir '卸载作业清单.lnk') $psExe $psArgs '卸载作业清单' $icon $installDir
    }

    Write-Step '创建开机启动快捷方式…'
    $startup = [Environment]::GetFolderPath('Startup')
    New-Shortcut (Join-Path $startup ($appName + '.lnk')) $psExe $startupArgs ($appName + ' · 开机启动') $icon $installDir

    Write-Step '注册卸载信息…'
    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HomeworkDock'
    if (-not (Test-Path -LiteralPath $uninstallKey)) {
        New-Item -Path $uninstallKey -Force | Out-Null
    }
    $uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $uninstallInInstall + '"'
    $sizeKb = [int]([math]::Ceiling(((Get-Item -LiteralPath (Join-Path $installDir 'HomeworkDock.dll')).Length + (Get-Item -LiteralPath $launcher).Length) / 1024))
    Set-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value $appName
    Set-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value '1.1.0'
    Set-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value '本机'
    Set-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $installDir
    Set-ItemProperty -Path $uninstallKey -Name 'DisplayIcon' -Value $icon
    Set-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value $uninstallCommand
    Set-ItemProperty -Path $uninstallKey -Name 'QuietUninstallString' -Value ($uninstallCommand + ' -Quiet')
    New-ItemProperty -Path $uninstallKey -Name 'EstimatedSize' -Value $sizeKb -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name 'NoModify' -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name 'NoRepair' -Value 1 -PropertyType DWord -Force | Out-Null

    Write-Host ''
    Write-Host '  安装完成！' -ForegroundColor Green
    Write-Host ('  安装位置：' + $installDir) -ForegroundColor Gray
    Write-Host '  已创建：桌面快捷方式、开始菜单、开机启动' -ForegroundColor Gray
    Write-Host ('  作业数据保存在：' + (Join-Path $env:APPDATA 'HomeworkDock')) -ForegroundColor Gray
    Write-Host ''

    if (-not $NoLaunch) {
        Write-Step '正在启动作业清单…'
        Start-Process -FilePath $psExe -ArgumentList $runArgs -WorkingDirectory $installDir -WindowStyle Hidden
    }
}
catch {
    Write-Host ''
    Write-Host ('  安装失败：' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ''
    exit 1
}
