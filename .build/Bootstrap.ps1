param (
    [string]$Action="default",
	[hashtable]$properties=@{},
    [switch]$Help
)

$Here = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"


$MSBuild  = exec { . $Here\vswhere.exe -latest -products * -requires Microsoft.Component.MSBuild -property installationPath }
if ($MSBuild) {
  $MSBuild  = join-path $MSBuild 'MSBuild\15.0\Bin\MSBuild.exe'
}

Write-Host "$MSBuild"


Import-Module "$Here\Common"

Install-Chocolatey

Install-Psake

$psakeDirectory = (Resolve-Path $env:ChocolateyInstall\lib\Psake*)

 if(!(Test-Path $psakeDirectory)) 
 {
    Import-Module (Join-Path $psakeDirectory "tools\Psake.psm1")
 }

if($Help)
{ 
	try 
	{
		Write-Host "Available build tasks:"
		psake -nologo -docs | Out-Host -paging
	} 
	catch {}

	return
}

Invoke-Psake -buildFile "$Here\Default.ps1" -parameters $properties -tasklist $Action