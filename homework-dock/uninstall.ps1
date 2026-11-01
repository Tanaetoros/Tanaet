param(
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$appName = '作业清单'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\HomeworkDock'
$dataDir = Join-Path $env:APPDATA 'HomeworkDock'

try {
    Write-Host ''
    Write-Host ('  ' + $appName + ' · 卸载程序') -ForegroundColor White
    Write-Host '  -------------------------' -ForegroundColor DarkGray
    Write-Host ''

    $deleteData = $false
    if (-not $Quiet) {
        Write-Host ('  作业数据位置：' + $dataDir) -ForegroundColor Gray
        $answer = Read-Host '  是否同时删除所有作业数据？输入 y 确认，直接回车保留数据'
        if ($answer -match '^(y|yes|是)$') {
            $deleteData = $true
        }
    }

    $running = Get-Process -Name 'HomeworkDock' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host '  正在关闭作业清单…' -ForegroundColor Gray
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
    }

    # The 1.1 build runs inside a hidden Windows PowerShell host. Stop only
    # our own host (run.ps1 in the install directory), never other PowerShell
    # windows the user may have open.
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
    Start-Sleep -Milliseconds 500

    $desktop = [Environment]::GetFolderPath('Desktop')
    $startup = [Environment]::GetFolderPath('Startup')
    $programs = [Environment]::GetFolderPath('Programs')
    $shortcuts = @(
        (Join-Path $desktop ($appName + '.lnk')),
        (Join-Path $startup ($appName + '.lnk'))
    )
    foreach ($path in $shortcuts) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }

    $menuDir = Join-Path $programs $appName
    if (Test-Path -LiteralPath $menuDir) {
        Remove-Item -LiteralPath $menuDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HomeworkDock'
    if (Test-Path -LiteralPath $uninstallKey) {
        Remove-Item -LiteralPath $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($deleteData -and (Test-Path -LiteralPath $dataDir)) {
        Remove-Item -LiteralPath $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    $removed = $false
    if (Test-Path -LiteralPath $installDir) {
        try {
            Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction Stop
            $removed = $true
        } catch {
            $removed = $false
        }
    } else {
        $removed = $true
    }

    if (-not $removed) {
        $cleanup = 'ping 127.0.0.1 -n 3 >nul & rmdir /s /q "' + $installDir + '"'
        Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $cleanup -WindowStyle Hidden | Out-Null
    }

    Write-Host ''
    Write-Host '  卸载完成。' -ForegroundColor Green
    if ($deleteData) {
        Write-Host '  作业数据已删除。' -ForegroundColor Gray
    } else {
        Write-Host ('  作业数据已保留：' + $dataDir) -ForegroundColor Gray
        Write-Host '  如果以后不再需要，可以手动删除这个文件夹。' -ForegroundColor Gray
    }
    Write-Host ''

    if (-not $Quiet) {
        Read-Host '  按 Enter 关闭' | Out-Null
    }
}
catch {
    Write-Host ''
    Write-Host ('  卸载失败：' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ''
    if (-not $Quiet) {
        Read-Host '  按 Enter 关闭' | Out-Null
    }
    exit 1
}
