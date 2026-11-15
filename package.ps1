$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$commit = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Run this script inside a Git checkout.' }
$branch = (git branch --show-current).Trim()
$output = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$archive = Join-Path $output 'IT-2514_Kuanyshpayev_Azamat_FinalExam.zip'
git archive --format=zip --output=$archive $commit
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Update)
try {
    $entry = $zip.CreateEntry('SUBMISSION_VERSION.txt')
    $writer = New-Object IO.StreamWriter($entry.Open())
    try {
        $writer.WriteLine('Repository: https://github.com/Askomm/Final_Project')
        $writer.WriteLine('Branch: ' + $branch)
        $writer.WriteLine('Commit: ' + $commit)
        $writer.WriteLine('All project files were exported directly from this commit.')
    } finally { $writer.Dispose() }
} finally { $zip.Dispose() }
Write-Output $archive
