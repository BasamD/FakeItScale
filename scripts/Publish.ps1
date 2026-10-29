$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet run --project tests/ScaleSim.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }
    dotnet publish src/ScaleSim.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    # Exercise the actual packaged Windows executable, including control construction.
    $process = Start-Process -FilePath (Resolve-Path 'artifacts/win-x64/ScaleSim.exe') -ArgumentList '--smoke-test' -PassThru
    if (-not $process.WaitForExit(30000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw 'Windows launch smoke test timed out.'
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        $errorLog = Join-Path $env:LOCALAPPDATA 'ScaleSim/startup-error.log'
        if (Test-Path $errorLog) { Get-Content $errorLog | Write-Output }
        throw "Windows launch smoke test failed: exit $($process.ExitCode)."
    }
    Write-Output 'PASS: packaged Windows executable displayed its main window and closed cleanly.'
    $staging = 'artifacts/ScaleSim-Windows-x64'
    New-Item -ItemType Directory -Force $staging | Out-Null
    Copy-Item artifacts/win-x64/ScaleSim.exe $staging
    Copy-Item README.md "$staging/README.txt"
    Copy-Item docs/WINDOWS-ACCEPTANCE.md "$staging/WINDOWS-ACCEPTANCE.txt"
    Copy-Item scripts/Test-Tcp.ps1 $staging
    Compress-Archive -Path "$staging/*" -DestinationPath artifacts/ScaleSim-Windows-x64.zip -Force
    Get-FileHash artifacts/ScaleSim-Windows-x64.zip -Algorithm SHA256
} finally { Pop-Location }
