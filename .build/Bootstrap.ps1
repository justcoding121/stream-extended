param (
    [string]$Action="default",
	[hashtable]$properties=@{},
    [switch]$Help
)

$Here = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"


$MSBuild  = & $Here\vswhere.exe -latest -products * -requires Microsoft.Component.MSBuild -property installationPath 
if ($MSBuild) {
  $MSBuild  = join-path $MSBuild 'MSBuild\15.0\Bin\MSBuild.exe'
}

Write-Host "Writing MSBuild"

Write-Host "$MSBuild"

Write-Host "Finish Writing MSBuild"
