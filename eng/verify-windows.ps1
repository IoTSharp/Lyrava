#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(10, 300)]
    [int]$StepTimeoutSeconds = 120,
    [ValidateRange(30, 1200)]
    [int]$TotalTimeoutSeconds = 600,
    [ValidateRange(2, 60)]
    [int]$StartupSeconds = 8
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$totalClock = [System.Diagnostics.Stopwatch]::StartNew()
$dotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
$expectedSdk = '10.0.404'
$windowsTfm = 'net10.0-windows10.0.19041.0'
$appProject = 'src/Lyrava.App/Lyrava.App.csproj'
$appSolution = 'Lyrava.App.slnx'
$outputDirectory = Join-Path $repoRoot 'src/Lyrava.App/bin/Release/net10.0-windows10.0.19041.0/win-x64'
$appExecutable = Join-Path $outputDirectory 'Lyrava.App.exe'

Write-Host "PowerShell $($PSVersionTable.PSVersion)"
Write-Host "Windows TFM $windowsTfm"

function Get-RemainingMilliseconds {
    $remaining = $TotalTimeoutSeconds * 1000 - $totalClock.ElapsedMilliseconds
    if ($remaining -le 0) {
        throw 'The Windows verification deadline has expired.'
    }

    return [int][Math]::Min($StepTimeoutSeconds * 1000, $remaining)
}

function New-ProcessStartInfo {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$Arguments,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [switch]$UseShellExecute
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $UseShellExecute.IsPresent
    $startInfo.CreateNoWindow = -not $UseShellExecute.IsPresent
    if (-not $UseShellExecute.IsPresent) {
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $startInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
        $startInfo.Environment['MSBUILDDISABLENODEREUSE'] = '1'
        $startInfo.Environment['DOTNET_CLI_USE_MSBUILD_SERVER'] = '0'
        $startInfo.Environment['UseSharedCompilation'] = 'false'
        $startInfo.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
        $startInfo.Environment['DOTNET_NOLOGO'] = '1'
    }

    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    return $startInfo
}

function Invoke-CommandStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $deadlineMilliseconds = Get-RemainingMilliseconds
    $startInfo = New-ProcessStartInfo -FilePath $FilePath -Arguments $Arguments -WorkingDirectory $repoRoot
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    $stepClock = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $process.Start()) {
            throw "Could not start $Name."
        }

        $started = $true
        $startUtc = $process.StartTime.ToUniversalTime().ToString('O')
        Write-Host "RUN $Name PID=$($process.Id) parent=$PID startedUtc=$startUtc"
        Write-Host "$FilePath $($Arguments -join ' ')"
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()

        $maximumPolls = [int][Math]::Ceiling($deadlineMilliseconds / 250.0) + 1
        for ($poll = 0; $poll -lt $maximumPolls; $poll++) {
            if ($process.WaitForExit(250)) {
                break
            }

            if ($stepClock.ElapsedMilliseconds -ge $deadlineMilliseconds) {
                throw "$Name exceeded its deadline ($deadlineMilliseconds ms)."
            }
        }

        if (-not $process.HasExited) {
            throw "$Name did not exit within its bounded polling budget."
        }

        $standardOutput = $stdout.WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult()
        $standardError = $stderr.WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult()
        if ($standardOutput.Length -gt 0) {
            Write-Host $standardOutput
        }
        if ($standardError.Length -gt 0) {
            Write-Host $standardError
        }
        if ($process.ExitCode -ne 0) {
            throw "$Name failed with exit code $($process.ExitCode)."
        }

        Write-Host "PASS $Name exit=0 elapsedMs=$($stepClock.ElapsedMilliseconds)"
        return [pscustomobject]@{
            Name = $Name
            ExitCode = 0
            Output = $standardOutput
        }
    }
    finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) {
                Write-Warning "Owned process PID=$($process.Id) has not confirmed exit."
            }
        }

        $process.Dispose()
    }
}

function Get-ProcessEvidence {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId
    )

    $filter = "ProcessId=$ProcessId"
    $processRecord = Get-CimInstance -ClassName Win32_Process -Filter $filter -ErrorAction SilentlyContinue
    if ($null -eq $processRecord) {
        return [pscustomobject]@{ ParentProcessId = 'UNKNOWN'; CommandLine = 'UNKNOWN' }
    }

    $commandLine = if ([string]::IsNullOrWhiteSpace($processRecord.CommandLine)) { 'UNKNOWN' } else { $processRecord.CommandLine }
    return [pscustomobject]@{
        ParentProcessId = $processRecord.ParentProcessId
        CommandLine = $commandLine
    }
}

function Invoke-StartupSmoke {
    $deadlineMilliseconds = Get-RemainingMilliseconds
    if (-not (Test-Path -LiteralPath $appExecutable -PathType Leaf)) {
        throw "Windows app executable was not found: $appExecutable"
    }

    $startInfo = New-ProcessStartInfo -FilePath $appExecutable -Arguments @() -WorkingDirectory $outputDirectory -UseShellExecute
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    $stepClock = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $process.Start()) {
            throw 'Could not start the unpackaged Windows app.'
        }

        $started = $true
        $evidence = Get-ProcessEvidence -ProcessId $process.Id
        $startUtc = $process.StartTime.ToUniversalTime().ToString('O')
        Write-Host "RUN unpackaged-startup PID=$($process.Id) parent=$($evidence.ParentProcessId) startedUtc=$startUtc"
        Write-Host "COMMAND $($evidence.CommandLine)"

        $startupMilliseconds = $StartupSeconds * 1000
        $maximumPolls = [int][Math]::Ceiling([Math]::Min($startupMilliseconds, $deadlineMilliseconds) / 250.0) + 1
        for ($poll = 0; $poll -lt $maximumPolls; $poll++) {
            if ($process.HasExited) {
                throw "The unpackaged Windows app exited before the $StartupSeconds second startup observation."
            }

            if ($stepClock.ElapsedMilliseconds -ge $startupMilliseconds) {
                break
            }

            if ($stepClock.ElapsedMilliseconds -ge $deadlineMilliseconds) {
                throw "The unpackaged startup observation exceeded its deadline ($deadlineMilliseconds ms)."
            }

            Start-Sleep -Milliseconds 250
        }

        if ($process.HasExited) {
            throw 'The unpackaged Windows app exited before the startup observation completed.'
        }

        Write-Host "PASS unpackaged-startup observedAliveSeconds=$StartupSeconds elapsedMs=$($stepClock.ElapsedMilliseconds)"
    }
    finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) {
                Write-Warning "Owned unpackaged app PID=$($process.Id) has not confirmed exit."
            }
            else {
                Write-Host "PASS unpackaged-startup cleanup=owned-process-tree-exited"
            }
        }

        $process.Dispose()
    }
}

$sdkResult = Invoke-CommandStep -Name sdk-version -FilePath $dotnetPath -Arguments @('--version')
$actualSdk = $sdkResult.Output.Trim()
if ($actualSdk -ne $expectedSdk) {
    throw "Expected .NET SDK $expectedSdk but found $actualSdk."
}
Write-Host "PASS sdk-version expected=$expectedSdk"

$null = Invoke-CommandStep -Name windows-restore -FilePath $dotnetPath -Arguments @('restore', $appSolution, '--disable-parallel', '--disable-build-servers')
$null = Invoke-CommandStep -Name windows-release-build -FilePath $dotnetPath -Arguments @('build', $appProject, '-f', $windowsTfm, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-nodeReuse:false', '-p:UseSharedCompilation=false')
$null = Invoke-CommandStep -Name app-format -FilePath $dotnetPath -Arguments @('format', $appSolution, '--verify-no-changes', '--no-restore', '--verbosity', 'minimal')
Invoke-StartupSmoke

Write-Host 'BOUNDARY real-source-playback=NOT_RUN'
Write-Host 'BOUNDARY system-media-panel-interaction=NOT_RUN'
Write-Host 'BOUNDARY MSIX-install-upgrade=NOT_RUN'
Write-Host 'BOUNDARY long-running-daily-use=NOT_RUN'
Write-Host "PASS Windows code/build/startup verification elapsedMs=$($totalClock.ElapsedMilliseconds)"
