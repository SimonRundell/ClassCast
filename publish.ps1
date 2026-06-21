<#
.SYNOPSIS
    Publishes ClassCast.Teacher and ClassCast.Student as self-contained win-x64 executables.
.DESCRIPTION
    Produces two standalone output folders under .\dist\ that include the .NET 8 runtime
    and all dependencies. No .NET installation is required on the target machine.
    ClassCast.Teacher also includes ffmpeg\ffmpeg.exe automatically via the project's
    CopyToOutputDirectory setting.
.OUTPUTS
    .\dist\Teacher\  — ClassCast Teacher app + runtime + ffmpeg
    .\dist\Student\  — ClassCast Student app + runtime
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root    = $PSScriptRoot
$config  = 'Release'
$rid     = 'win-x64'
$distDir = Join-Path $root 'dist'

# Clean previous output
if (Test-Path $distDir) {
    Write-Host "Cleaning $distDir ..." -ForegroundColor Yellow
    Remove-Item $distDir -Recurse -Force
}

$projects = @(
    @{ Name = 'Teacher'; Path = Join-Path $root 'ClassCast.Teacher\ClassCast.Teacher.csproj' },
    @{ Name = 'Student'; Path = Join-Path $root 'ClassCast.Student\ClassCast.Student.csproj' }
)

foreach ($proj in $projects) {
    $outDir = Join-Path $distDir $proj.Name
    Write-Host ""
    Write-Host "Publishing ClassCast.$($proj.Name) -> $outDir" -ForegroundColor Cyan

    dotnet publish $proj.Path `
        --configuration $config `
        --runtime $rid `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        --output $outDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed for $($proj.Name). Exit code: $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    # Report output size
    $sizeMB = [math]::Round((Get-ChildItem $outDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Write-Host "  Done. Output size: ${sizeMB} MB" -ForegroundColor Green
}

Write-Host ""
Write-Host "All builds complete. Output in: $distDir" -ForegroundColor Green
Write-Host ""
Write-Host "To distribute:" -ForegroundColor Yellow
Write-Host "  Compress-Archive -Path dist\Teacher -DestinationPath ClassCastTeacher.zip"
Write-Host "  Compress-Archive -Path dist\Student -DestinationPath ClassCastStudent.zip"
