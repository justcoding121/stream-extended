$PSake.use_exit_on_error = $true

$Here = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$SolutionRoot = (Split-Path -parent $Here)

$ProjectName = "SslStreamExtended"

$SolutionFile = "$SolutionRoot\$ProjectName.sln"

## This comes from the build server iteration
if(!$BuildNumber) { $BuildNumber = $env:APPVEYOR_BUILD_NUMBER }
if(!$BuildNumber) { $BuildNumber = "1"}

## This comes from the Hg commit hash used to build
if(!$CommitHash) { $CommitHash = $env:APPVEYOR_REPO_COMMIT }
if(!$CommitHash) { $CommitHash = "local-build" }

## The build configuration, i.e. Debug/Release
if(!$Configuration) { $Configuration = $env:Configuration }
if(!$Configuration) { $Configuration = "Release" }

if(!$Version) { $Version = $env:APPVEYOR_BUILD_VERSION }
if(!$Version) { $Version = "1.0.$BuildNumber" }

if(!$Branch) { $Branch = $env:APPVEYOR_REPO_BRANCH }
if(!$Branch) { $Branch = "local" }
if($Branch -eq "beta" ) { $Version = "$Version-beta" }

Import-Module "$Here\Common" -DisableNameChecking

$NuGet = Join-Path $SolutionRoot ".nuget\nuget.exe"

$MSBuild ="${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\msbuild.exe"

FormatTaskName (("-"*25) + "[{0}]" + ("-"*25))

Task default -depends Clean, Build, Package

Task Build {
	exec { . $MSBuild $SolutionFile /t:Build /v:normal /p:Configuration=$Configuration }
}

Task Package -depends Build {
	exec { . $NuGet pack "$SolutionRoot\SslStreamExtended\SslStreamExtended.nuspec" -Properties Configuration=$Configuration -OutputDirectory "$SolutionRoot" -Version "$Version" }
}

Task Clean -depends Install-BuildTools {
	Get-ChildItem .\ -include bin,obj -Recurse | foreach ($_) { Remove-Item $_.fullname -Force -Recurse }
	exec { . $MSBuild $SolutionFile /t:Clean /v:quiet }
}


Task Install-MSBuild {
    if(!(Test-Path "${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\msbuild.exe")) 
	{ 
		cinst microsoft-build-tools -y
	}
}

Task Install-BuildTools -depends Install-MSBuild
