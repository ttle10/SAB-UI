# =========================================
# SabProfiles Old → New JSON Converter
# =========================================

$inputPath  = "sabProfiles.json"
$outputPath = "sabProfiles.new.json"

# Load old JSON
$oldData = Get-Content $inputPath -Raw | ConvertFrom-Json

function Convert-PiccField($value)
{
    if ($value -eq "Aucun") {
        return @{
            value  = "Aucun"
            status = "Disconnected"
        }
    }
    else {
        return @{
            value  = $value
            status = "Connected"
        }
    }
}

function Convert-NotMonitoredField($value)
{
    return @{
        value  = $value
        status = "NotMonitored"
    }
}

function Convert-Endpoint($oldEndpoint)
{
    return @{
        piccNames = Convert-PiccField $oldEndpoint.poste
        profileNames = @{
            value  = "Aucun"
            status = "Disconnected"
        }
        cra  = Convert-NotMonitoredField $oldEndpoint.cra
        reu  = Convert-NotMonitoredField $oldEndpoint.reu
        sgcz = Convert-NotMonitoredField $oldEndpoint.sgcz
        sti  = Convert-NotMonitoredField $oldEndpoint.sti
    }
}

$newData = foreach ($item in $oldData)
{
    @{
        index       = $item.index
        profile     = $item.profile
        displayName = $item.displayname
        ccp         = Convert-Endpoint $item.ccp
        ccr         = Convert-Endpoint $item.ccr
    }
}

# Write new JSON
$newData |
    ConvertTo-Json -Depth 10 |
    Set-Content $outputPath -Encoding UTF8

Write-Host "✅ SabProfiles conversion completed!"
Write-Host "➡ Input : $inputPath"
Write-Host "➡ Output: $outputPath"