# ==============================
# SabPex JSON Conversion Script
# ==============================

$inputPath  = "sabPexs.json"
$outputPath = "sabPexs.new.json"

# Load old JSON
$oldData = Get-Content $inputPath -Raw | ConvertFrom-Json

function Convert-Endpoint($oldEndpoint)
{
    return @{
        piccNames = @{
            value  = $oldEndpoint.picc
            status = if ($oldEndpoint.picc -eq "Aucun") { "Disconnected" } else { "Connected" }
        }
        profileNames = @{
            value  = "Aucun"
            status = "Disconnected"
        }
        cra = @{
            value  = $oldEndpoint.cra
            status = "NotMonitored"
        }
        reu = @{
            value  = $oldEndpoint.reu
            status = "NotMonitored"
        }
        sgcz = @{
            value  = $oldEndpoint.sgcz
            status = "NotMonitored"
        }
        sti = @{
            value  = $oldEndpoint.sti
            status = "NotMonitored"
        }
    }
}

$newData = foreach ($item in $oldData)
{
    @{
        index        = $item.index
        displayName  = $item.displayname
        ccpHostname  = $item.ccphostname
        ccrHostname  = $item.ccrhostname
        ccp          = Convert-Endpoint $item.ccp
        ccr          = Convert-Endpoint $item.ccr
    }
}

# Save formatted JSON
$newData |
    ConvertTo-Json -Depth 10 |
    Set-Content $outputPath -Encoding UTF8

Write-Host "✅ Conversion complete!"
Write-Host "➡ Input : $inputPath"
Write-Host "➡ Output: $outputPath"