$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Resolve-Path "$scriptRoot\.." | Select-Object -ExpandProperty Path
$searchRoot = "$projectRoot"

$files = Get-ChildItem -Path $searchRoot -Recurse -Include *Tests*.cs -File | Sort-Object FullName
$total = $files.Count
$batchSize = 20
$totalBatches = [math]::Ceiling($total / $batchSize)
Write-Host "Total files: $total, Batches: $totalBatches"

for ($b = 1; $b -le $totalBatches; $b++) {
    Write-Host "Running batch $b/$totalBatches"
    & "$projectRoot\scripts\batch-add-test-docs.ps1" -BatchSize $batchSize -BatchNumber $b
}

Write-Host "All batches processed — regenerating report"
& "$projectRoot\scripts\generate-test-doc-report.ps1"

exit 0
