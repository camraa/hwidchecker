# Compile "HWID Checker.exe" avec le compilateur C# fourni par Windows. Rien a installer.
$ErrorActionPreference = 'Stop'
$fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$src = Join-Path $PSScriptRoot 'src'
$out = Join-Path $PSScriptRoot 'HWID Checker.exe'

if (-not (Test-Path "$src\app.ico")) { & "$PSScriptRoot\make-icon.ps1" }

$refs = @('System.dll', 'System.Core.dll', 'Microsoft.CSharp.dll', 'System.Management.dll', 'System.Web.Extensions.dll', 'System.Windows.Forms.dll',
          'System.Drawing.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll',
          'WPF\UIAutomationTypes.dll', 'WPF\UIAutomationProvider.dll') | ForEach-Object { "/reference:$fw\$_" }

& "$fw\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /platform:anycpu /main:HwidChecker.App `
    "/out:$out" "/win32icon:$src\app.ico" "/win32manifest:$src\app.manifest" `
    "/resource:$src\MainWindow.xaml,MainWindow.xaml" "/resource:$src\app.ico,app.ico" `
    $refs "$src\App.cs" "$src\Hardware.cs" "$src\Security.cs" "$src\Drivers.cs" "$src\Vendors.cs" "$src\Help.cs" "$src\Lang.cs"
if ($LASTEXITCODE -ne 0) { throw 'Echec de la compilation' }
Get-Item $out | Select-Object Name, Length
