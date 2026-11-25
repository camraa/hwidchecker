# Compile "HWID Checker.exe" avec le compilateur C# fourni par Windows. Rien a installer.
# -NoAdmin : version de test, sans demande de droits administrateur, dans le dossier indique.
param([string]$NoAdmin)
$ErrorActionPreference = 'Stop'
$fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$src = Join-Path $PSScriptRoot 'src'
$out = Join-Path $PSScriptRoot 'HWID Checker.exe'

$manifest = "$src\app.manifest"
if ($NoAdmin) {
    $out = Join-Path $NoAdmin 'HWID Checker.exe'
    $manifest = Join-Path $NoAdmin 'test.manifest'
    [IO.File]::WriteAllText($manifest, [IO.File]::ReadAllText("$src\app.manifest").Replace('requireAdministrator', 'asInvoker'))
}

if (-not (Test-Path "$src\app.ico")) { & "$PSScriptRoot\make-icon.ps1" }

$refs = @('System.dll', 'System.Core.dll', 'Microsoft.CSharp.dll', 'System.Management.dll', 'System.Web.Extensions.dll', 'System.Windows.Forms.dll',
          'System.Drawing.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll',
          'WPF\UIAutomationTypes.dll', 'WPF\UIAutomationProvider.dll') | ForEach-Object { "/reference:$fw\$_" }

& "$fw\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /platform:anycpu /main:HwidChecker.App `
    "/out:$out" "/win32icon:$src\app.ico" "/win32manifest:$manifest" `
    "/resource:$src\MainWindow.xaml,MainWindow.xaml" "/resource:$src\app.ico,app.ico" `
    $refs (Get-ChildItem "$src\*.cs" | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) { throw 'Echec de la compilation' }
Get-Item $out | Select-Object Name, Length
