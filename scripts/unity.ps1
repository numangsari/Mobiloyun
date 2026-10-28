# Runs Jelly Mold's Unity tasks in batch mode (Windows PowerShell 5+ / PowerShell 7).
#
#   powershell -ExecutionPolicy Bypass -File scripts\unity.ps1 setup|test-edit|test-play|screenshots|perf|apk|all
#
# Unity is located from JellyMold\ProjectSettings\ProjectVersion.txt in the default Unity Hub
# install folder; set $env:UNITY_PATH to Unity.exe to override. Results and logs go to
# JellyMold\Builds\ (git-ignored).
param([string]$Task = "all")
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "JellyMold"
$Out = Join-Path $Project "Builds"
New-Item -ItemType Directory -Force -Path (Join-Path $Out "logs") | Out-Null

function Find-Unity {
    if ($env:UNITY_PATH) { return $env:UNITY_PATH }
    $versionLine = Get-Content (Join-Path $Project "ProjectSettings\ProjectVersion.txt") | Where-Object { $_ -like "m_EditorVersion:*" }
    $version = ($versionLine -replace "m_EditorVersion:\s*", "").Trim()
    $bases = @("$env:ProgramFiles\Unity\Hub\Editor", "${env:ProgramFiles(x86)}\Unity\Hub\Editor", "$env:LOCALAPPDATA\Unity\Hub\Editor")
    foreach ($base in $bases) {
        if (-not (Test-Path $base)) { continue }
        $dirs = @((Join-Path $base $version)) + @(Get-ChildItem $base -Directory -Filter "6000.*" | Sort-Object Name -Descending | ForEach-Object { $_.FullName })
        foreach ($dir in $dirs) {
            $exe = Join-Path $dir "Editor\Unity.exe"
            if (Test-Path $exe) { return $exe }
        }
    }
    throw "Unity 6 not found. Install it with Unity Hub or set `$env:UNITY_PATH."
}

$Unity = Find-Unity
Write-Host "Unity: $Unity"

function Invoke-Unity([string]$Name, [string[]]$UnityArgs) {
    Write-Host "==> $Name"
    $log = Join-Path $Out "logs\$Name.log"
    $all = @("-batchmode", "-projectPath", "`"$Project`"", "-logFile", "`"$log`"") + $UnityArgs
    $p = Start-Process -FilePath $Unity -ArgumentList $all -Wait -PassThru -WindowStyle Hidden
    if ($p.ExitCode -ne 0) { Write-Host "    FAILED (exit $($p.ExitCode)) - see $log" -ForegroundColor Red; return $false }
    Write-Host "    ok"
    return $true
}

function Invoke-Tests([string]$Name, [string]$Platform, [string]$Category, [string[]]$Extra = @()) {
    $xml = Join-Path $Out "$Name.xml"
    # Önceki çalışmanın sonucu, yeni denemenin yerine geçmemeli.
    if (Test-Path -LiteralPath $xml) { Remove-Item -LiteralPath $xml -Force }
    $testArgs = @("-runTests", "-testPlatform", $Platform, "-testResults", "`"$xml`"") + $Extra
    if ($Category) { $testArgs += @("-testCategory", $Category) }
    if (-not (Invoke-Unity $Name $testArgs)) { return $false }
    if (-not (Test-Path $xml)) { Write-Host "    no results file" -ForegroundColor Red; return $false }
    try {
        $run = ([xml](Get-Content -LiteralPath $xml -Raw)).DocumentElement
        if ($run.Name -ne "test-run") { throw "Geçersiz sonuç dosyası." }
        $counts = @{}
        foreach ($attribute in @("total", "passed", "failed", "skipped", "inconclusive")) {
            [long]$count = 0
            if (-not [long]::TryParse($run.GetAttribute($attribute), [ref]$count) -or $count -lt 0) {
                throw "Geçersiz sonuç sayısı: $attribute"
            }
            $counts[$attribute] = $count
        }
        Write-Host "    total=$($counts.total) passed=$($counts.passed) failed=$($counts.failed) skipped=$($counts.skipped) inconclusive=$($counts.inconclusive)"
        # Atlanan veya hiç çalışmayan kontroller başarı sayılmaz.
        return ($run.result -eq "Passed" -and $counts.total -gt 0 -and $counts.passed -eq $counts.total -and $counts.failed -eq 0 -and $counts.skipped -eq 0 -and $counts.inconclusive -eq 0)
    } catch {
        Write-Host "    Sonuç okunamadı: $_" -ForegroundColor Red
        return $false
    }
}

function Invoke-Task([string]$Name) {
    switch ($Name) {
        "setup"       { return Invoke-Unity "setup" @("-quit", "-executeMethod", "JellyMold.EditorTools.ProjectSetup.Apply") }
        "test-edit"   { return Invoke-Tests "test-edit" "EditMode" "" @("-nographics") }
        "test-play"   { return Invoke-Tests "test-play" "PlayMode" "Smoke" }
        "screenshots" { $ok = Invoke-Tests "screenshots" "PlayMode" "Capture"; Write-Host "    images: $Out\Screenshots"; return $ok }
        "perf"        { $ok = Invoke-Tests "perf" "PlayMode" "Perf"; if ($ok -and (Test-Path "$Out\perf.json")) { Get-Content "$Out\perf.json" | Out-Host }; return $ok }
        "apk"         {
            $ok = Invoke-Unity "apk" @("-quit", "-buildTarget", "Android", "-executeMethod", "JellyMold.EditorTools.LevelTools.BuildAndroid")
            Get-ChildItem "$Out\Android\*.apk" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ("    {0} ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) }
            return $ok
        }
        default { throw "usage: unity.ps1 setup|test-edit|test-play|screenshots|perf|apk|all" }
    }
}

if ($Task -eq "all") {
    $failed = @()
    foreach ($t in @("setup", "test-edit", "test-play", "screenshots", "perf", "apk")) {
        if (-not (Invoke-Task $t)) { $failed += $t }
    }
    if ($failed.Count -gt 0) { Write-Host "Failed: $($failed -join ', ')" -ForegroundColor Red; exit 1 }
    Write-Host "All tasks succeeded." -ForegroundColor Green
} else {
    if (-not (Invoke-Task $Task)) { exit 1 }
}
