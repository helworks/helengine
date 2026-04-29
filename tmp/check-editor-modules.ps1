$exe = 'C:\dev\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe'
$arg = 'C:\dev\helprojs\city\project.heproj'
$log = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), 'helengine.editor.startup.log')
if (Test-Path $log) { Remove-Item $log -Force }
$proc = Start-Process -FilePath $exe -ArgumentList @($arg) -PassThru
Start-Sleep -Seconds 5
$proc.Refresh()
$moduleNames = @()
try {
  $moduleNames = $proc.Modules | ForEach-Object { $_.ModuleName }
} catch {
  $moduleNames = @('MODULE_READ_FAILED: ' + $_.Exception.Message)
}
$threads = @()
try {
  $threads = $proc.Threads | ForEach-Object {
    [PSCustomObject]@{ Id = $_.Id; ThreadState = $_.ThreadState.ToString(); WaitReason = $_.WaitReason.ToString() }
  }
} catch {
  $threads = @([PSCustomObject]@{ Id = -1; ThreadState = 'FAILED'; WaitReason = $_.Exception.Message })
}
$mainWindowHandle = 0
if ($null -ne $proc.MainWindowHandle) {
  $mainWindowHandle = [int64]$proc.MainWindowHandle
}
$result = [PSCustomObject]@{
  Alive = -not $proc.HasExited
  Id = $proc.Id
  MainWindowHandle = $mainWindowHandle
  MainWindowTitle = if ($null -ne $proc.MainWindowTitle) { $proc.MainWindowTitle } else { '' }
  Temp = [System.IO.Path]::GetTempPath()
  StartupLogExists = Test-Path $log
  StartupLog = if (Test-Path $log) { Get-Content $log } else { @() }
  Modules = $moduleNames
  Threads = $threads
}
$result | ConvertTo-Json -Depth 6
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
