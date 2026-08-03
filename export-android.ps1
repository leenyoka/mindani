$GODOT   = "C:\Users\Linda\Downloads\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe"
$PROJECT = "C:\Dev\mindani"
$CSPROJ  = Join-Path $PROJECT "Mindani.csproj"
$APK     = Join-Path $PROJECT "build\mindani.apk"
$LOG     = Join-Path $env:TEMP "mindani_export.log"

# Godot rewrites Mindani.csproj with net8.0 on startup, so we lock it to net9.0 during export.
$CSPROJ_CONTENT = @'
<Project Sdk="Godot.NET.Sdk/4.7.1">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="scripts/**/*.cs" />
  </ItemGroup>
</Project>
'@

Write-Host ""
Write-Host "==============================" -ForegroundColor Cyan
Write-Host "  Mindani Android APK Export" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

# 1. Lock csproj to net9.0
Write-Host "[1/3] Locking Mindani.csproj to net9.0..." -ForegroundColor Yellow
Set-ItemProperty $CSPROJ -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue
[System.IO.File]::WriteAllText($CSPROJ, $CSPROJ_CONTENT, [System.Text.Encoding]::UTF8)
Set-ItemProperty $CSPROJ -Name IsReadOnly -Value $true
Write-Host "      Locked." -ForegroundColor Gray

# 2. Run Godot export; kill it when done (Godot does not exit after headless export)
Write-Host "[2/3] Running Godot export (about 5 minutes)..." -ForegroundColor Yellow
"" | Set-Content $LOG

$proc = Start-Process `
    -FilePath $GODOT `
    -ArgumentList "--headless", "--path", $PROJECT, "--export-debug", "Android", $APK `
    -RedirectStandardError $LOG `
    -PassThru -NoNewWindow

$lastLine = ""
$deadline = [DateTime]::Now.AddMinutes(10)

while ([DateTime]::Now -lt $deadline) {
    Start-Sleep -Seconds 3
    if ($proc.HasExited) { break }

    if (Test-Path $LOG) {
        $text = Get-Content $LOG -Raw -ErrorAction SilentlyContinue
        if (-not $text) { continue }

        $recent = ($text -split "`n" |
            Where-Object { $_ -match "^\[" -or $_ -match "ADDING|Signing|Signed|Verifying|DONE" } |
            Select-Object -Last 1)
        if ($recent) {
            $recent = $recent.Trim()
            if ($recent -ne $lastLine) {
                Write-Host "      $recent" -ForegroundColor Gray
                $lastLine = $recent
            }
        }

        if ($text -match "Signed`r?`n") {
            Start-Sleep -Seconds 2
            break
        }
    }
}

if (-not $proc.HasExited) { $proc.Kill() }

# 3. Restore csproj
Set-ItemProperty $CSPROJ -Name IsReadOnly -Value $false
Write-Host "      Mindani.csproj unlocked." -ForegroundColor Gray

# Check for export errors
if (Test-Path $LOG) {
    $logText = Get-Content $LOG -Raw
    if ($logText -match "export.*failed|Cannot export|configuration errors") {
        Write-Host ""
        Write-Host "FAILED: export errors detected." -ForegroundColor Red
        Get-Content $LOG | Where-Object { $_ -match "error|failed|ERROR" } | Select-Object -First 10
        Write-Host "Full log: $LOG" -ForegroundColor Gray
        exit 1
    }
}

# 4. Verify APK
Write-Host "[3/3] Verifying APK..." -ForegroundColor Yellow
if (Test-Path $APK) {
    $apkItem = Get-Item $APK
    $sizeMB  = [math]::Round($apkItem.Length / 1MB, 1)
    Write-Host ""
    Write-Host "SUCCESS" -ForegroundColor Green
    Write-Host "  File : $($apkItem.FullName)" -ForegroundColor White
    Write-Host "  Size : $sizeMB MB" -ForegroundColor White
    Write-Host "  Built: $($apkItem.LastWriteTime)" -ForegroundColor White
    Write-Host ""
    Write-Host "Install on device:" -ForegroundColor Cyan
    $adb = "C:\Users\Linda\AppData\Local\Android\Sdk\platform-tools\adb.exe"
    Write-Host "  & `"$adb`" install `"$APK`"" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "FAILED: APK not found at $APK" -ForegroundColor Red
    Write-Host "Full log: $LOG" -ForegroundColor Yellow
    exit 1
}
