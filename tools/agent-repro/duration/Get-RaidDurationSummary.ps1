# 汇总一个或多个时长批次：逐局输出状态、有效时长、开箱数、双人结局和带出价值，并给出合并统计。
param([Parameter(Mandatory = $true)][string[]]$Batches)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$root = Join-Path $source 'Logs\RaidDuration'
$prices = $null
$all = @()
foreach ($batch in @($Batches | ForEach-Object { $_.Split(',') } | Where-Object { $_.Trim() })) {
    $dir = Join-Path $root $batch.Trim()
    $study = Get-Content (Join-Path $dir 'study.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $rows = @()
    foreach ($run in Get-ChildItem -LiteralPath $dir -Directory | Where-Object Name -like 'run-*' | Sort-Object { [int]($_.Name -replace '\D', '') }) {
        $resultPath = Join-Path $run.FullName 'result.json'
        if (!(Test-Path $resultPath)) { $rows += [pscustomobject][ordered]@{run=$run.Name; status='MISSING'}; continue }
        $r = Get-Content $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $cfg = Get-Content (Join-Path $run.FullName 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $events = Get-Content (Join-Path $run.FullName 'events.jsonl') -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json }
        $opened = @($events | Where-Object kind -eq 'inventory.opened').Count
        $outcome = @{}
        $capacity = 0
        foreach ($e in @($events | Where-Object kind -eq 'route.result')) {
            $d = $e.detail | ConvertFrom-Json
            if ($d.stage -eq 'Extracted') { $outcome[$d.agent] = 'X@' + [math]::Round([double]$e.gameSeconds) }
            elseif ($d.stage -eq 'Dead' -and -not $outcome.ContainsKey($d.agent)) { $outcome[$d.agent] = 'D@' + [math]::Round([double]$e.gameSeconds) }
            if ($d.reason -eq 'CapacityExtraction') { $capacity++ }
        }
        if ($null -eq $prices) {
            # 物品售价取自本局导出的物品定义（字段名兼容大小写）
            $prices = @{}
            $defs = Get-Content (Join-Path $run.FullName 'item-definitions.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $stack = New-Object System.Collections.Stack; $stack.Push($defs)
            while ($stack.Count -gt 0) {
                $o = $stack.Pop()
                if ($o -is [System.Array]) { foreach ($x in $o) { $stack.Push($x) }; continue }
                if ($o -is [psobject]) {
                    $names = $o.PSObject.Properties.Name
                    $idName = $names | Where-Object { $_ -match '^(itemId|ItemID|id)$' } | Select-Object -First 1
                    $priceName = $names | Where-Object { $_ -match '^(sellPrice|SellPrice)$' } | Select-Object -First 1
                    if ($idName -and $priceName) { $prices[[string]$o.$idName] = [double]$o.$priceName }
                    foreach ($p in $o.PSObject.Properties) { if ($p.Value -is [psobject] -or $p.Value -is [System.Array]) { $stack.Push($p.Value) } }
                }
            }
        }
        $items = 0; $value = 0
        $warehouse = Join-Path $run.FullName 'warehouse-final.json'
        if (Test-Path $warehouse) {
            $w = Get-Content $warehouse -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($player in $w.Players) { foreach ($page in $player.Pages) { foreach ($item in $page.Items) {
                $items += [int]$item.Amount
                if ($prices.ContainsKey([string]$item.ItemID)) { $value += $prices[[string]$item.ItemID] * [int]$item.Amount } } } }
        }
        $game = [double]$r.elapsedGameSeconds
        $rows += [pscustomobject][ordered]@{run=$run.Name; seed=$cfg.seed; status=$r.status; game=[math]::Round($game, 1); boxes=$opened;
            effective=[math]::Round($game + 7 * $opened, 1); a1=$outcome['1']; a2=$outcome['2']; capacityExtract=$capacity;
            items=$items; value=$value; errors=$r.errors; wall=[math]::Round([double]$r.elapsedWallSeconds, 1)}
    }
    "=== $batch head=$($study.sourceHead.Substring(0, 8)) edits=$(@($study.edits).Count)"
    foreach ($row in $rows) { ($row.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ' }
    $all += $rows
}
$done = @($all | Where-Object { $_.status -like 'RAID_OBSERVED_*' })
$effective = @($done | ForEach-Object { $_.effective } | Sort-Object)
$median = if ($effective.Count) { if ($effective.Count % 2) { $effective[[int](($effective.Count - 1) / 2)] } else { ($effective[$effective.Count / 2 - 1] + $effective[$effective.Count / 2]) / 2 } } else { $null }
$both = @($done | Where-Object { $_.a1 -like 'X*' -and $_.a2 -like 'X*' }).Count
$deaths = @($done | ForEach-Object { @($_.a1, $_.a2) } | Where-Object { $_ -like 'D*' }).Count
$mean = if ($effective.Count) { [math]::Round(($effective | Measure-Object -Average).Average, 1) } else { '' }
$meanValue = if ($done.Count) { [math]::Round(($done | ForEach-Object { $_.value } | Measure-Object -Average).Average, 0) } else { '' }
"SUMMARY runs=$($done.Count)/$($all.Count) medianEffective=$median meanEffective=$mean min=$($effective | Select-Object -First 1) max=$($effective | Select-Object -Last 1) bothExtracted=$both deaths=$deaths meanValue=$meanValue"
