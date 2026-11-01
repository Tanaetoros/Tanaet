$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$log = Join-Path $env:TEMP 'HomeworkDock-host.log'

try {
    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase
    Add-Type -AssemblyName System.Xaml
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $dll = Join-Path $here 'HomeworkDock.dll'
    $env:HOMEWORKDOCK_HOME = $here
    # Load from bytes instead of LoadFrom: .NET treats a mapped/network drive
    # as a remote zone and refuses to load assemblies from it.
    [void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($dll))
    [HomeworkDock.HomeworkApplication]::Run([string[]]$args)
}
catch {
    $message = ('[{0}] {1}{2}{3}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $_.Exception.Message, [Environment]::NewLine, $_.ScriptStackTrace)
    try { Add-Content -LiteralPath $log -Value $message -Encoding UTF8 } catch { }
    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show(
            ('作业清单启动失败：' + $_.Exception.Message + [Environment]::NewLine + [Environment]::NewLine + '日志：' + $log),
            '作业清单', 'OK', 'Error') | Out-Null
    } catch { }
    exit 1
}
