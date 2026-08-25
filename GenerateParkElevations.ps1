# Generates ParkElevations.csv - a lookup table of elevation (in feet) for every
# active US POTA park, built by querying the free Open-Elevation API.
#
# This only needs to be run once to get started, and re-run occasionally
# (say, every few months) to pick up newly-added parks. It can take a while
# (POTA has tens of thousands of US parks) - just let it run. Progress is
# printed as it goes, and results are written to disk incrementally, so if it
# gets interrupted partway through you won't lose everything that's already
# been done - just re-run it and it will pick up where the output file left
# off skipped.

$ErrorActionPreference = "Stop"

$ParksUrl = "https://pota.app/all_parks_ext.csv"
$ElevationApiUrl = "https://api.open-elevation.com/api/v1/lookup"
$OutputFile = "ParkElevations.csv"
$BatchSize = 100
$MinBatchSize = 5
$DelayBetweenBatchesMs = 1000
$MaxRetries = 3

# ---- Step 1: figure out what's already done, so re-runs can resume ----
$alreadyDone = @{}
if (Test-Path $OutputFile) {
    Write-Host "Found existing $OutputFile - will skip parks already in it."
    Import-Csv $OutputFile | ForEach-Object { $alreadyDone[$_.Reference] = $true }
    Write-Host "Already have $($alreadyDone.Count) parks."
} else {
    "Reference,ElevationFeet" | Out-File -FilePath $OutputFile -Encoding utf8
}

# ---- Step 2: download and parse the POTA master park list ----
Write-Host "Downloading POTA park list..."
$csvText = (Invoke-WebRequest -Uri $ParksUrl -UseBasicParsing).Content
$lines = $csvText -split "`n"

# Simple CSV line parser that handles quoted fields with embedded commas,
# matching the same parsing the main app itself uses on this same file.
function Parse-CsvLine([string]$line) {
    $fields = New-Object System.Collections.Generic.List[string]
    $sb = New-Object System.Text.StringBuilder
    $inQuotes = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $c = $line[$i]
        if ($inQuotes) {
            if ($c -eq '"') {
                if ($i + 1 -lt $line.Length -and $line[$i + 1] -eq '"') {
                    [void]$sb.Append('"')
                    $i++
                } else {
                    $inQuotes = $false
                }
            } else {
                [void]$sb.Append($c)
            }
        } else {
            if ($c -eq '"') { $inQuotes = $true }
            elseif ($c -eq ',') { $fields.Add($sb.ToString()); [void]$sb.Clear() }
            else { [void]$sb.Append($c) }
        }
    }
    $fields.Add($sb.ToString())
    return $fields
}

$parks = New-Object System.Collections.Generic.List[object]
for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i].Trim("`r", "`n")
    if ([string]::IsNullOrWhiteSpace($line)) { continue }

    $fields = Parse-CsvLine $line
    if ($fields.Count -lt 8) { continue }

    $reference = $fields[0]
    $active = $fields[2]
    $latStr = $fields[5]
    $lonStr = $fields[6]

    if ($active -ne "1") { continue }
    if (-not $reference.StartsWith("US-")) { continue }
    if ($alreadyDone.ContainsKey($reference)) { continue }

    $lat = 0.0; $lon = 0.0
    if (-not [double]::TryParse($latStr, [ref]$lat)) { continue }
    if (-not [double]::TryParse($lonStr, [ref]$lon)) { continue }

    $parks.Add([PSCustomObject]@{ Reference = $reference; Lat = $lat; Lon = $lon })
}

Write-Host "Found $($parks.Count) active US parks still needing elevation."

if ($parks.Count -eq 0) {
    Write-Host "Nothing to do - ParkElevations.csv is already up to date."
    exit 0
}

# ---- Step 3: query Open-Elevation in batches, appending results as we go ----
# Open-Elevation is a free, shared public server - a batch that's too large
# can time out on their end (504 Gateway Timeout) even when nothing is wrong
# on this end. Rather than just giving up after a few retries, a batch that
# fails gets split in half and each half is tried separately, continuing to
# shrink down to $MinBatchSize before finally giving up on that chunk. This
# means occasional server overload gets worked around automatically instead
# of requiring you to re-run the whole script repeatedly.

function Send-Batch([array]$batch) {
    $locations = $batch | ForEach-Object { @{ latitude = $_.Lat; longitude = $_.Lon } }
    $body = @{ locations = $locations } | ConvertTo-Json -Depth 5 -Compress

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $ElevationApiUrl -Method Post `
                -ContentType "application/json" -Body $body -TimeoutSec 90

            $rows = New-Object System.Collections.Generic.List[string]
            for ($j = 0; $j -lt $batch.Count; $j++) {
                $meters = $response.results[$j].elevation
                $feet = [Math]::Round($meters * 3.28084)
                $rows.Add("$($batch[$j].Reference),$feet")
            }
            $rows -join "`r`n" | Out-File -FilePath $OutputFile -Append -Encoding utf8
            return $true
        } catch {
            Write-Host "    Attempt $attempt (batch size $($batch.Count)) failed: $($_.Exception.Message)"
            if ($attempt -lt $MaxRetries) { Start-Sleep -Seconds (3 * $attempt) }
        }
    }
    return $false
}

function Process-Chunk([array]$chunk) {
    if (Send-Batch $chunk) {
        return $chunk.Count
    }

    if ($chunk.Count -le $MinBatchSize) {
        Write-Host "  Giving up on $($chunk.Count) park(s) after repeated failures at minimum batch size - re-run the script later to retry them: $(($chunk | ForEach-Object { $_.Reference }) -join ', ')"
        return 0
    }

    Write-Host "  Splitting batch of $($chunk.Count) in half and retrying each half..."
    $half = [Math]::Ceiling($chunk.Count / 2)
    $firstHalf = $chunk[0..($half - 1)]
    $secondHalf = $chunk[$half..($chunk.Count - 1)]

    $completed = 0
    $completed += Process-Chunk $firstHalf
    Start-Sleep -Milliseconds $DelayBetweenBatchesMs
    $completed += Process-Chunk $secondHalf
    return $completed
}

$total = $parks.Count
$done = 0
$succeeded = 0
$batchNum = 0
$totalBatches = [Math]::Ceiling($total / $BatchSize)

for ($start = 0; $start -lt $total; $start += $BatchSize) {
    $batchNum++
    $end = [Math]::Min($start + $BatchSize, $total) - 1
    $batch = $parks[$start..$end]

    $succeeded += Process-Chunk $batch

    $done += $batch.Count
    $pct = [Math]::Round(($done / $total) * 100)
    Write-Host "Batch $batchNum / $totalBatches - $done / $total parks processed ($pct%), $succeeded succeeded so far"

    Start-Sleep -Milliseconds $DelayBetweenBatchesMs
}

Write-Host ""
Write-Host "Done. Results are in $OutputFile."
Write-Host "Copy this file into your project (same place as KffCrossReference.csv) and it'll pick it up automatically."
