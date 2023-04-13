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
    $Release = $false
)

$Folder = "c:\home\pi\bin\"

if (-not (Test-Path $Folder)) {
    New-Item $Folder -ItemType Directory
}

$Runtime = "linux-arm"

if (-not $NoBuild) {
    Get-ChildItem $Folder | Remove-Item -Recurse

    if ($Release) {
        dotnet publish .\src\Resona.UI\Resona.UI.csproj -o $Folder -r "linux-arm" -c Release -f net7.0 -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=false --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true
    }
    else {
        dotnet publish .\src\Resona.UI\Resona.UI.csproj -o $Folder -r "linux-arm" -c Debug -f net7.0 --self-contained true
    }
}

if ($NoClean) {
    scp $(Join-Path $Folder "Resona*") "$($Server):bin"
}
else {
    ssh $Server "cd ~/bin && find . -type f ! -name 'resona.db*' | xargs rm -f"
    scp -r -p c:\home\pi\bin "$($Server):"
}

ssh $Server 'chmod +x ~/bin/Resona'