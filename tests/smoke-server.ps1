# Starts a built server with a tiny resource that reads a Race-style map
# (GTA.Math.Vector3 through XmlSerializer) and checks that the resource starts.
#
#   pwsh tests/smoke-server.ps1 -ServerDir bin/Release/Server
#
# For a single-file publish, point -ReferenceDir at a normal build output so the
# smoke resource has RageCoop.Server.dll and friends to compile against.
#
# The server is run from a temporary copy, so -ServerDir is left untouched.
# Exits 0 when the resource logged SMOKE-OK, 1 otherwise.
param(
    [Parameter(Mandatory = $true)][string]$ServerDir,
    [string]$ReferenceDir,
    [int]$TimeoutSeconds = 90,
    [string]$Label = 'ServerSmoke'
)

$ErrorActionPreference = 'Stop'
$ServerDir = (Resolve-Path $ServerDir).Path
$ReferenceDir = if ($ReferenceDir) { (Resolve-Path $ReferenceDir).Path } else { $ServerDir }
$onWindows = $env:OS -eq 'Windows_NT'
$exeName = if ($onWindows) { 'RageCoop.Server.exe' } else { 'RageCoop.Server' }

if (-not (Test-Path (Join-Path $ServerDir $exeName))) {
    Write-Output "::error title=$Label::$exeName not found in $ServerDir"
    exit 1
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("ragecoop-smoke-" + [System.Guid]::NewGuid().ToString('N'))
Copy-Item $ServerDir $work -Recurse
$proc = $null
try {
    $resDir = Join-Path $work 'Resources/Server/StubSmoke'
    dotnet build (Join-Path $PSScriptRoot 'ServerSmoke/StubSmoke.csproj') -c Release -o $resDir "-p:ServerDir=$ReferenceDir" --nologo -v q | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Output "::error title=$Label::Could not build the smoke resource"
        exit 1
    }

    $outputs = @((Join-Path $work 'RageCoop.Server.log'), (Join-Path $work 'smoke-stdout.txt'), (Join-Path $work 'smoke-stderr.txt'))
    Remove-Item $outputs[0] -ErrorAction SilentlyContinue
    $proc = Start-Process -FilePath (Join-Path $work $exeName) -WorkingDirectory $work -PassThru `
        -RedirectStandardOutput $outputs[1] -RedirectStandardError $outputs[2]

    $text = ''
    $done = 'SMOKE-OK|SMOKE-FAIL|Failed to start resource: StubSmoke|Failed to load resource: StubSmoke|Fatal error occurred'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        Start-Sleep -Seconds 2
        $text = ''
        foreach ($f in $outputs) {
            if (Test-Path $f) {
                try {
                    $fs = [System.IO.File]::Open($f, 'Open', 'Read', 'ReadWrite')
                    $sr = New-Object System.IO.StreamReader($fs)
                    $text += $sr.ReadToEnd() + "`n"
                    $sr.Dispose()
                } catch { }
            }
        }
        if ($text -match $done -or $proc.HasExited -or (Get-Date) -ge $deadline) { break }
    }

    if ($text -match 'SMOKE-OK') {
        Write-Output "::notice title=$Label::OK - the server started a resource that reads GTA.Math.Vector3 from XML"
        exit 0
    }

    $state = if ($proc.HasExited) { "server exited with code $($proc.ExitCode)" } else { 'server still running' }
    $lines = ($text -split "`n") | ForEach-Object { $_.Trim() } |
        Where-Object { $_ -match 'ERR|Exception|SMOKE|Fatal|error|not found|Unhandled' } | Select-Object -Unique -First 6
    $detail = if ($lines) { $lines -join ' // ' }
              elseif ($text.Trim()) { 'no SMOKE-OK line within the time limit: ' + ((($text.Trim() -split "`n") | Select-Object -Last 4) -join ' // ') }
              else { 'the server produced no output' }
    Write-Output ("::error title=$Label::($state) $detail" -replace "`r", '')
    exit 1
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1 }
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
