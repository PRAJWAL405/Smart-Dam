# ==============================================================================
# Smart Dam Control System — STQA Automated Test Runner & HTML Report Generator
# ==============================================================================

Write-Host "`n[1/3] Executing STQA Test Suite via .NET Test Runner..." -ForegroundColor Cyan

$dotnet = "dotnet"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
}

$resultsDir = Join-Path $PSScriptRoot "tests\DamControlSystem.Tests\TestResults"
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
}

$trxFile = Join-Path $resultsDir "test_results.trx"
$reportHtml = Join-Path $PSScriptRoot "TestReport.html"

# Run dotnet test with TRX logger
& $dotnet test "$PSScriptRoot\DamControlSystem.sln" --logger "trx;LogFileName=test_results.trx"

$exitCode = $LASTEXITCODE

Write-Host "`n[2/3] Generating Structured STQA HTML Requirement Report..." -ForegroundColor Cyan

# Parse TRX file and generate HTML Report
if (Test-Path $trxFile) {
    [xml]$trx = Get-Content $trxFile
    $ns = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
    $ns.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")

    $results = $trx.SelectNodes("//t:UnitTestResult", $ns)
    $total = $results.Count
    $passed = ($results | Where-Object { $_.outcome -eq "Passed" }).Count
    $failed = ($results | Where-Object { $_.outcome -eq "Failed" }).Count
    $failedColor = if ($failed -gt 0) { "Red" } else { "Gray" }
    
    Write-Host "`n------------------------------------------------------------" -ForegroundColor Green
    Write-Host " STQA TEST EXECUTION SUMMARY" -ForegroundColor Green
    Write-Host "------------------------------------------------------------" -ForegroundColor Green
    Write-Host " Total Test Cases : $total" -ForegroundColor White
    Write-Host " Passed           : $passed" -ForegroundColor Green
    Write-Host " Failed           : $failed" -ForegroundColor $failedColor
    if ($total -gt 0) {
        $rate = [math]::Round(($passed / $total) * 100, 1)
        Write-Host " Pass Rate        : $rate%" -ForegroundColor Green
    }
    Write-Host "------------------------------------------------------------" -ForegroundColor Green
    Write-Host " HTML Report      : $reportHtml" -ForegroundColor Cyan
    Write-Host "------------------------------------------------------------`n" -ForegroundColor Green

    # Run generator test to refresh TestReport.html
    & $dotnet test "$PSScriptRoot\DamControlSystem.sln" --filter "FullyQualifiedName~Generate_StqaHtmlReport" --nologo | Out-Null
}

Write-Host "[3/3] Done. To view the HTML report in your browser:" -ForegroundColor Yellow
Write-Host "      Start-Process TestReport.html`n" -ForegroundColor White

exit $exitCode
