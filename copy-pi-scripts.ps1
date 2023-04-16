[CmdletBinding()]
param (
    [Parameter()]
    $Server = "pi@dev2"
)

# Copy the Resona scripts to the server
scp -r "./scripts/*" "$($Server):"

# Use SSH to mark the scripts as executable 
ssh $Server "chmod +x *.sh"
