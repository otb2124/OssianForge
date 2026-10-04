param (
    [switch]$CleanOld
)

# 1. Determine Project Name from .csproj / .sln or Fallback to Root Folder Name
$RootPath = (Get-Item -Path ".").FullName
$ProjectName = (Get-Item -Path ".").Name

$CsprojFile = Get-ChildItem -Path $RootPath -Filter "*.csproj" -Recurse | Select-Object -First 1
if ($CsprojFile) {
    $ProjectName = [System.IO.Path]::GetFileNameWithoutExtension($CsprojFile.Name)
}

# Clean project name for valid filename output
$CleanProjectName = $ProjectName -replace '[\\/:*?"<>|]', '-'

# 2. Setup Exports Folder and Timestamped Filename
$ExportsFolder = Join-Path -Path $RootPath -ChildPath "exports"
if (-not (Test-Path $ExportsFolder)) {
    New-Item -ItemType Directory -Path $ExportsFolder | Out-Null
}

# Optional Cleanup of Previous Exports
if ($CleanOld) {
    Write-Host "Cleaning up previous export files in $ExportsFolder..." -ForegroundColor Yellow
    Get-ChildItem -Path $ExportsFolder -Filter "*.xml" | Remove-Item -Force
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$OutputFileName = "$CleanProjectName-codebase-$Timestamp.xml"
$OutputFilePath = Join-Path -Path $ExportsFolder -ChildPath $OutputFileName

Write-Host "Exporting .NET Core / EF Core codebase to $OutputFilePath..." -ForegroundColor Green

$StringBuilder = [System.Text.StringBuilder]::new()

# 3. System Prompt Header for the LLM
[void]$StringBuilder.AppendLine("<codebase_context>")
[void]$StringBuilder.AppendLine("<overview>")
[void]$StringBuilder.AppendLine("This document contains the complete source code, directory structure, and configuration files for a .NET Core / ASP.NET Core application using Entity Framework Core.")
[void]$StringBuilder.AppendLine("Use this context to analyze, refactor, debug, or generate code matching the project architecture, C# patterns, and EF Core models.")
[void]$StringBuilder.AppendLine("</overview>")
[void]$StringBuilder.AppendLine()

# 4. Directory Tree Representation (Excludes build artifacts and exports folder)
[void]$StringBuilder.AppendLine("<directory_structure>")
Get-ChildItem -Recurse | 
    Where-Object { 
        $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|exports)[\\/]'
    } | 
    ForEach-Object {
        $relativePath = $_.FullName.Substring($RootPath.Length + 1).Replace("\", "/")
        [void]$StringBuilder.AppendLine($relativePath)
    }
[void]$StringBuilder.AppendLine("</directory_structure>")
[void]$StringBuilder.AppendLine()

# 5. Source Code & Configuration Files Content
[void]$StringBuilder.AppendLine("<files>")

Get-ChildItem -Recurse -Include "*.cs", "*.cshtml", "*.razor", "*.csproj", "*.sln", "appsettings*.json", "*.md" | 
    Where-Object { 
        # Exclude directories and build/tooling folders
        -not $_.PSIsContainer -and
        $_.FullName -notmatch '[\\/](bin|obj|\.git|\.vs|exports)[\\/]' -and
        # Exclude generated EF Core migration snapshots and designer files
        $_.Name -notmatch '\.Designer\.cs$' -and
        $_.Name -notmatch 'ModelSnapshot\.cs$'
    } | 
    ForEach-Object {
        $relativePath = $_.FullName.Substring($RootPath.Length + 1).Replace("\", "/")
        
        Write-Host "Processing: $relativePath" -ForegroundColor Gray
        
        [void]$StringBuilder.AppendLine("  <file path=""$relativePath"">")
        $content = Get-Content $_.FullName -Raw -Encoding UTF8
        
        if ($content) {
            # Escape raw closing tags if present inside files
            $safeContent = $content.Replace("</file>", "<\_file>")
            [void]$StringBuilder.AppendLine($safeContent)
        }
        
        [void]$StringBuilder.AppendLine("  </file>")
        [void]$StringBuilder.AppendLine()
    }

[void]$StringBuilder.AppendLine("</files>")
[void]$StringBuilder.AppendLine("</codebase_context>")

# 6. Save File
[System.IO.File]::WriteAllText($OutputFilePath, $StringBuilder.ToString(), [System.Text.Encoding]::UTF8)

Write-Host "Export completed successfully! Saved to $OutputFilePath" -ForegroundColor Green