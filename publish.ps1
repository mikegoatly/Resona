[CmdletBinding()]
param (
    [Parameter()]
    [switch]
    $NoBuild,

    [Parameter()]
    $Server = "pi@dev2",

    [switch]
    $NoClean = $false,

    [switch]
    $DebugBuild = $false
)

$Folder = "./publish"
$Project = ".\src\Resona.UI\Resona.UI.csproj"

if (-not (Test-Path $Folder)) {
    New-Item $Folder -ItemType Directory
}

$Runtime = "linux-arm"

#>>> Build
if (-not $NoBuild) {
    Get-ChildItem $Folder | Remove-Item -Recurse

    if ($DebugBuild) {
        dotnet publish $Project -o $Folder -r "linux-arm" -c Debug -f net7.0 --self-contained true
    }
    else {
        dotnet publish $Project -o $Folder -r "linux-arm" -c Release -f net7.0 -p:PublishReadyToRun=true `
            -p:PublishSingleFile=true -p:PublishTrimmed=false `
            --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true
    }
}

#>>> Publish Resona
# Create the bin folder, if it doesn't already exist
ssh $Server "mkdir -p ~/bin"

# Disable the service (if installed) so we can replace the executable
ssh $Server "./disable-auto-run.sh"

if ($NoClean) {
    scp $(Join-Path $Folder "Resona*") "$($Server):bin"
}
else {
    ssh $Server "cd ~/bin && find . -type f ! -name 'resona.db*' | xargs rm -f"
    scp -r -p "$($Folder)/*" "$($Server):bin"
}

# Name Resona executable
ssh $Server 'chmod +x ~/bin/Resona'

# Re-enable the service (if installed). This will also auto start the program.
ssh $Server "./reenable-auto-run.sh"