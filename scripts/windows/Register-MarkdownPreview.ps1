[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$Unregister
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$previewHandlerGuid = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$windowsTextPreviewerClsid = '{1531d583-8375-4d3f-b5fb-d23bbd169f22}'
$extensions = @(
    @{ Extension = '.md'; ContentType = 'text/markdown' },
    @{ Extension = '.markdown'; ContentType = 'text/markdown' }
)

function Assert-WindowsTextPreviewerExists {
    $clsidPath = "Registry::HKEY_CLASSES_ROOT\CLSID\$windowsTextPreviewerClsid"
    if (-not (Test-Path $clsidPath)) {
        throw "Windows TXT Previewer was not found at $clsidPath."
    }
}

function Set-DefaultValue {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    if ($Path -notlike 'HKCU:\*') {
        throw "Set-DefaultValue only supports HKCU registry paths. Received: $Path"
    }

    $subKeyPath = $Path.Substring('HKCU:\'.Length)
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($subKeyPath, $true)
    if ($null -eq $key) {
        throw "Registry key was not found: $Path"
    }

    try {
        $key.SetValue('', $Value, [Microsoft.Win32.RegistryValueKind]::String)
    }
    finally {
        $key.Dispose()
    }
}

function Register-MarkdownPreview {
    Assert-WindowsTextPreviewerExists

    foreach ($item in $extensions) {
        $extension = $item.Extension
        $extensionPath = "HKCU:\Software\Classes\$extension"
        $shellExPath = Join-Path $extensionPath "shellex\$previewHandlerGuid"

        if ($PSCmdlet.ShouldProcess($extension, 'register Windows TXT Previewer for Explorer preview pane')) {
            New-Item -Path $extensionPath -Force | Out-Null
            New-ItemProperty -Path $extensionPath -Name 'Content Type' -Value $item.ContentType -PropertyType String -Force | Out-Null
            New-ItemProperty -Path $extensionPath -Name 'PerceivedType' -Value 'text' -PropertyType String -Force | Out-Null

            New-Item -Path $shellExPath -Force | Out-Null
            Set-DefaultValue -Path $shellExPath -Value $windowsTextPreviewerClsid
        }
    }
}

function Unregister-MarkdownPreview {
    foreach ($item in $extensions) {
        $extension = $item.Extension
        $extensionPath = "HKCU:\Software\Classes\$extension"
        $shellExPath = Join-Path $extensionPath "shellex\$previewHandlerGuid"

        if ($PSCmdlet.ShouldProcess($extension, 'remove MarkdownConverter Explorer preview registration')) {
            if (Test-Path $shellExPath) {
                Remove-Item -LiteralPath $shellExPath -Recurse -Force
            }

            foreach ($propertyName in @('Content Type', 'PerceivedType')) {
                $property = Get-ItemProperty -Path $extensionPath -Name $propertyName -ErrorAction SilentlyContinue
                if ($null -ne $property) {
                    Remove-ItemProperty -Path $extensionPath -Name $propertyName -ErrorAction SilentlyContinue
                }
            }
        }
    }
}

if ($Unregister) {
    Unregister-MarkdownPreview
    Write-Host 'Markdown preview registration removed for the current user.'
}
else {
    Register-MarkdownPreview
    Write-Host 'Markdown preview registration added for the current user. Restart File Explorer if the preview pane does not update immediately.'
}
