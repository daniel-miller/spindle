dotnet run

$Source = "..\..\dist\output\Service\Content\Files\Data"
$Target = "C:\base\repo\insite\code\src\library\Shift.Service\Content\Files\Data"

# Specify which subdirectories to move
$SubdirectoriesToMove = @("TFile", "TFileActivity", "TFileClaim")

# Only proceed if source directory exists
if (Test-Path $Source) 
{
    foreach ($Subdirectory in $SubdirectoriesToMove) 
    {
        $SourcePath = Join-Path $Source $Subdirectory
        $TargetPath = Join-Path $Target $Subdirectory
        
        # Check if source subdirectory exists before attempting to move
        if (Test-Path $SourcePath) 
        {
            # Remove target if it exists to ensure clean overwrite
            if (Test-Path $TargetPath) 
            {
                Remove-Item $TargetPath -Recurse -Force
                Write-Host "Removed existing target: $TargetPath"
            }
            
            # Move the subdirectory
            Move-Item $SourcePath $TargetPath -Force
            Write-Host "Moved: $SourcePath -> $TargetPath"

            $Entity = $Subdirectory.Substring(1)
            $SourceFile = "$($Entity)Service.cs"
            $SourcePath = Join-Path $Source $SourceFile

            if (Test-Path $SourcePath) 
            {
                Move-Item $SourcePath $Target -Force
            }
        } 
        else 
        {
            Write-Warning "Source subdirectory not found: $SourcePath"
        }
    }
} 
else 
{
    Write-Error "Source directory does not exist: $Source"
}