Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-NativeProcessArgument {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ($Value.Length -eq 0) {
        return '""'
    }

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    $EscapedValue = $Value -replace '(\\*)"', '$1$1\"'
    $EscapedValue = $EscapedValue -replace '(\\+)$', '$1$1'
    return '"' + $EscapedValue + '"'
}

function Invoke-StreamingNativeProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        throw "Native process path must be provided."
    }

    $StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $StartInfo.FileName = $FilePath
    $StartInfo.Arguments = (($ArgumentList | ForEach-Object { ConvertTo-NativeProcessArgument -Value $_ }) -join " ")
    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $true
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true

    $Process = New-Object System.Diagnostics.Process
    $Process.StartInfo = $StartInfo
    $OutputSubscription = $null
    $ErrorSubscription = $null
    try {
        $OutputSubscription = Register-ObjectEvent -InputObject $Process -EventName OutputDataReceived -Action {
            if ($null -ne $EventArgs.Data) {
                [Console]::Out.WriteLine($EventArgs.Data)
                [Console]::Out.Flush()
            }
        }
        $ErrorSubscription = Register-ObjectEvent -InputObject $Process -EventName ErrorDataReceived -Action {
            if ($null -ne $EventArgs.Data) {
                [Console]::Error.WriteLine($EventArgs.Data)
                [Console]::Error.Flush()
            }
        }

        if (-not $Process.Start()) {
            throw "Native process '$FilePath' failed to start."
        }

        $Process.BeginOutputReadLine()
        $Process.BeginErrorReadLine()
        $Process.WaitForExit()
        $Process.WaitForExit()
        return $Process.ExitCode
    } finally {
        if ($null -ne $OutputSubscription) {
            Unregister-Event -SubscriptionId $OutputSubscription.Id -ErrorAction SilentlyContinue
        }
        if ($null -ne $ErrorSubscription) {
            Unregister-Event -SubscriptionId $ErrorSubscription.Id -ErrorAction SilentlyContinue
        }
        $Process.Dispose()
    }
}

Export-ModuleMember -Function 'Invoke-StreamingNativeProcess'
