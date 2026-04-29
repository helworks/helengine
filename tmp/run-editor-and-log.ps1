$exe = 'C:\dev\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe'
$arg = 'C:\dev\helprojs\city\project.heproj'
$log = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), 'helengine.editor.startup.log')
if (Test-Path $log) { Remove-Item $log -Force }
$proc = Start-Process -FilePath $exe -ArgumentList @($arg) -PassThru
$proc.WaitForExit(5000) | Out-Null
$proc.Refresh()
$logLines = @()
if (Test-Path $log) {
  $logLines = [System.IO.File]::ReadAllLines($log)
}
[PSCustomObject]@{
  HasExited = $proc.HasExited
  ExitCode = if ($proc.HasExited) { $proc.ExitCode } else { $null }
  MainWindowHandle = if ($null -ne $proc.MainWindowHandle) { [int64]$proc.MainWindowHandle } else { 0 }
  MainWindowTitle = if ($null -ne $proc.MainWindowTitle) { $proc.MainWindowTitle } else { '' }
  LogLines = $logLines
} | ConvertTo-Json -Depth 4
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
