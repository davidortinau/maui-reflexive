# PowerShell relaunch script for Windows
# Builds the MAUI app, stages it, launches new instance, then kills old one.

$ErrorActionPreference = "Stop"

$ProjectDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectName = Split-Path -Leaf $ProjectDir
$BuildDir = Join-Path $ProjectDir "bin\Debug\net10.0-windows10.0.19041.0\win-x64"
$StagingDir = Join-Path $ProjectDir "bin\staging"

# Get old PIDs
$OldPids = Get-Process -Name $ProjectName -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id

Write-Host "🔨 Building..."
Set-Location $ProjectDir

$BuildOutput = & dotnet build "$ProjectName.csproj" -f net10.0-windows10.0.19041.0 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ BUILD FAILED!" -ForegroundColor Red
    Write-Host $BuildOutput | Select-String "error CS" -Context 0,5
    Write-Host "Old app instance remains running."
    exit 1
}

Write-Host ($BuildOutput -split "`n" | Select-Object -Last 3)

Write-Host "📦 Copying to staging..."
if (Test-Path (Join-Path $StagingDir $ProjectName)) {
    Remove-Item (Join-Path $StagingDir $ProjectName) -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
Copy-Item -Path $BuildDir -Destination (Join-Path $StagingDir $ProjectName) -Recurse

$ExePath = Join-Path $StagingDir "$ProjectName\$ProjectName.exe"

for ($attempt = 1; $attempt -le 2; $attempt++) {
    Write-Host "🚀 Launching new instance (attempt $attempt/2)..."
    $NewProcess = Start-Process -FilePath $ExePath -PassThru -WindowStyle Normal
    
    if ($null -eq $NewProcess) {
        Write-Host "⚠️ Failed to start."
        if ($attempt -lt 2) { Write-Host "🔁 Retrying..."; continue }
        Write-Host "Old instance left running."
        exit 1
    }
    
    Write-Host "✅ New instance running (PID $($NewProcess.Id))"
    Write-Host "🔎 Verifying stability for 8s..."
    
    $stable = $true
    for ($i = 0; $i -lt 8; $i++) {
        Start-Sleep -Seconds 1
        if ($NewProcess.HasExited) { $stable = $false; break }
    }
    
    if ($stable) {
        if ($OldPids) {
            Write-Host "🔪 Closing old instance(s)..."
            $OldPids | ForEach-Object {
                Write-Host "   Killing PID $_"
                Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
            }
        }
        Write-Host "✅ Handoff complete!"
        exit 0
    }
    
    Write-Host "❌ New instance crashed."
    if ($attempt -lt 2) { Write-Host "🔁 Retrying..."; continue }
}

Write-Host "⚠️ New instance unstable. Old instance left running."
exit 1
