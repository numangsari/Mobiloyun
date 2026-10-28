# Unity kurulu olmadan Windows denetim komutunun hata durumlarını doğrular.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$TestRoot = Join-Path $Root ("JellyMold\Builds\RunnerTests\" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
$FakeUnity = Join-Path $TestRoot "Unity Stub.exe"

Add-Type -OutputAssembly $FakeUnity -OutputType ConsoleApplication -TypeDefinition @'
using System;
using System.IO;
public static class UnityStub
{
    public static int Main(string[] args)
    {
        string scenario = Environment.GetEnvironmentVariable("JELLYMOLD_RUNNER_CASE");
        if (scenario == "missing" || scenario == "stale") return 0;
        int index = Array.IndexOf(args, "-testResults");
        if (index < 0 || index + 1 == args.Length) return 2;
        string xml = "<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\" inconclusive=\"0\" />";
        if (scenario == "zero") xml = "<test-run result=\"Passed\" total=\"0\" passed=\"0\" failed=\"0\" skipped=\"0\" inconclusive=\"0\" />";
        if (scenario == "skipped") xml = "<test-run result=\"Skipped\" total=\"1\" passed=\"0\" failed=\"0\" skipped=\"1\" inconclusive=\"0\" />";
        if (scenario == "inconclusive") xml = "<test-run result=\"Inconclusive\" total=\"1\" passed=\"0\" failed=\"0\" skipped=\"0\" inconclusive=\"1\" />";
        if (scenario == "failed") xml = "<test-run result=\"Failed\" total=\"2\" passed=\"1\" failed=\"1\" skipped=\"0\" inconclusive=\"0\" />";
        if (scenario == "malformed") xml = "<test-run";
        if (scenario == "wrongRoot") xml = "<test-suite result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\" inconclusive=\"0\" />";
        if (scenario == "missingCounter") xml = "<test-run result=\"Passed\" failed=\"0\" />";
        if (scenario == "negativeCounter") xml = "<test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"-1\" inconclusive=\"0\" />";
        File.WriteAllText(args[index + 1], xml);
        if (scenario == "perfFailure" || scenario == "perfPassed")
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[index + 1]), "perf.json"), "{}");
        return scenario == "processFailed" || scenario == "perfFailure" ? 1 : 0;
    }
}
'@

$PreviousUnity = $env:UNITY_PATH
$PreviousCase = $env:JELLYMOLD_RUNNER_CASE
$Failures = @()
try {
    $env:UNITY_PATH = $FakeUnity
    foreach ($case in @("passed", "processFailed", "missing", "stale", "zero", "skipped", "inconclusive", "failed", "malformed", "wrongRoot", "missingCounter", "negativeCounter", "perfFailure", "perfPassed")) {
        $caseRoot = Join-Path $TestRoot ("project " + $case)
        $scripts = Join-Path $caseRoot "scripts"
        New-Item -ItemType Directory -Path $scripts -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $Root "scripts\unity.ps1") -Destination $scripts
        if ($case -eq "stale") {
            $builds = Join-Path $caseRoot "JellyMold\Builds"
            New-Item -ItemType Directory -Path $builds -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $builds "test-edit.xml") -Value '<test-run result="Passed" total="1" passed="1" failed="0" skipped="0" inconclusive="0" />'
        }
        $env:JELLYMOLD_RUNNER_CASE = $case
        $task = if ($case.StartsWith("perf")) { "perf" } else { "test-edit" }
        $output = & "$PSHOME\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scripts "unity.ps1") $task 2>&1
        $expected = if ($case -eq "passed" -or $case -eq "perfPassed") { 0 } else { 1 }
        if ($LASTEXITCODE -eq $expected) {
            Write-Host "GEÇTİ: $case"
        } else {
            $Failures += $case
            Write-Host "BAŞARISIZ: $case (beklenen=$expected, alınan=$LASTEXITCODE)"
            Write-Host ($output -join [Environment]::NewLine)
        }
    }
} finally {
    $env:UNITY_PATH = $PreviousUnity
    $env:JELLYMOLD_RUNNER_CASE = $PreviousCase
}
if ($Failures.Count -gt 0) { throw "Başarısız durumlar: $($Failures -join ', ')" }
Write-Host "14 durum doğrulandı."
