#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(10, 300)]
    [int]$StepTimeoutSeconds = 120,
    [ValidateRange(30, 1200)]
    [int]$TotalTimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
Write-Host "PowerShell $($PSVersionTable.PSVersion)"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
$totalClock = [System.Diagnostics.Stopwatch]::StartNew()

function Invoke-VerificationStep {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    $remainingMilliseconds = [int][Math]::Min(
        $StepTimeoutSeconds * 1000,
        $TotalTimeoutSeconds * 1000 - $totalClock.ElapsedMilliseconds)
    if ($remainingMilliseconds -le 0) {
        throw 'The verification deadline has expired.'
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($dotnetPath)
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    # Do not leave reusable build/compiler servers behind after a successful step.
    $startInfo.Environment['MSBUILDDISABLENODEREUSE'] = '1'
    $startInfo.Environment['DOTNET_CLI_USE_MSBUILD_SERVER'] = '0'
    $startInfo.Environment['UseSharedCompilation'] = 'false'
    $startInfo.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $startInfo.Environment['DOTNET_NOLOGO'] = '1'
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $started = $false
    $stepClock = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $process.Start()) {
            throw "Could not start $Name."
        }

        $started = $true

        Write-Host "RUN $Name PID=$($process.Id) parent=$PID startedUtc=$($process.StartTime.ToUniversalTime().ToString('O'))"
        Write-Host "$dotnetPath $($Arguments -join ' ')"
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()

        # Poll count and elapsed time both bound the loop. Ctrl+C unwinds through finally.
        $maximumPolls = [int][Math]::Ceiling($remainingMilliseconds / 250.0) + 1
        for ($poll = 0; $poll -lt $maximumPolls; $poll++) {
            if ($process.WaitForExit(250)) {
                break
            }

            if ($stepClock.ElapsedMilliseconds -ge $remainingMilliseconds) {
                throw "$Name exceeded its deadline ($remainingMilliseconds ms)."
            }

            if (($poll + 1) % 40 -eq 0) {
                Write-Host "RUN $Name elapsedMs=$($stepClock.ElapsedMilliseconds)"
            }
        }

        if (-not $process.HasExited) {
            throw "$Name did not exit within its bounded polling budget."
        }

        Write-Host $stdout.WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult()
        $errorOutput = $stderr.WaitAsync([TimeSpan]::FromSeconds(5)).GetAwaiter().GetResult()
        if ($errorOutput.Length -gt 0) {
            Write-Host $errorOutput
        }

        if ($process.ExitCode -ne 0) {
            throw "$Name failed with exit code $($process.ExitCode)."
        }

        Write-Host "PASS $Name exit=0 elapsedMs=$($stepClock.ElapsedMilliseconds)"
    }
    finally {
        if ($started -and -not $process.HasExited) {
            # This handle is the exact process created above; never kill by name.
            $process.Kill($true)
            if (-not $process.WaitForExit(5000)) {
                Write-Warning "Owned process PID=$($process.Id) has not confirmed exit."
            }
        }

        $process.Dispose()
    }
}

# The same four application checks are used locally and by hosted CI.
Invoke-VerificationStep -Name restore -Arguments @('restore', 'Lyrava.slnx', '--disable-parallel', '--disable-build-servers')
Invoke-VerificationStep -Name build -Arguments @('build', 'Lyrava.slnx', '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-nodeReuse:false', '-p:UseSharedCompilation=false')
Invoke-VerificationStep -Name format -Arguments @('format', 'Lyrava.slnx', '--verify-no-changes', '--no-restore', '--verbosity', 'minimal')
Invoke-VerificationStep -Name test -Arguments @('test', 'Lyrava.slnx', '-c', 'Release', '--no-build', '--no-restore', '--disable-build-servers', '-m:1', '-nodeReuse:false', '--blame-hang-timeout', '30s', '--logger', 'console;verbosity=normal')
Write-Host "PASS portable verification elapsedMs=$($totalClock.ElapsedMilliseconds)"
