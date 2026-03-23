$ErrorActionPreference = 'Stop'

$workspace = 'C:\Users\USER\Desktop\Back-In-School\BackInSchool'
$qaDir = Join-Path $workspace 'QA'
$workbookPath = Get-ChildItem -Path $qaDir -File |
    Where-Object { $_.Extension -eq '.xlsx' -and $_.Name -notlike '*.bak' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$implementedPath = Join-Path $qaDir 'BackInSchool_TestCases_Implemented_v0.1.tsv'
$plannedPath = Join-Path $qaDir 'BackInSchool_TestCases_Planned_v0.1.tsv'

function Get-CellRef {
    param([int]$Row, [int]$Column)

    $name = ''
    $n = $Column
    while ($n -gt 0) {
        $m = ($n - 1) % 26
        $name = [char](65 + $m) + $name
        $n = [math]::Floor(($n - 1) / 26)
    }

    "$name$Row"
}

function New-InlineCell {
    param([xml]$Xml, [string]$CellRef, [string]$Value)

    $cell = $Xml.CreateElement('c', $Xml.DocumentElement.NamespaceURI)
    $null = $cell.SetAttribute('r', $CellRef)
    $null = $cell.SetAttribute('t', 'inlineStr')

    $is = $Xml.CreateElement('is', $Xml.DocumentElement.NamespaceURI)
    $t = $Xml.CreateElement('t', $Xml.DocumentElement.NamespaceURI)
    $t.InnerText = if ($null -eq $Value) { '' } else { [string]$Value }
    $null = $is.AppendChild($t)
    $null = $cell.AppendChild($is)
    $cell
}

function New-NumberCell {
    param([xml]$Xml, [string]$CellRef, [string]$Value)

    $cell = $Xml.CreateElement('c', $Xml.DocumentElement.NamespaceURI)
    $null = $cell.SetAttribute('r', $CellRef)
    $v = $Xml.CreateElement('v', $Xml.DocumentElement.NamespaceURI)
    $v.InnerText = $Value
    $null = $cell.AppendChild($v)
    $cell
}

function New-SheetRow {
    param([xml]$Xml, [int]$RowIndex, [object[]]$Values, [bool[]]$IsNumber)

    $row = $Xml.CreateElement('row', $Xml.DocumentElement.NamespaceURI)
    $null = $row.SetAttribute('r', [string]$RowIndex)

    for ($i = 0; $i -lt $Values.Count; $i++) {
        $cellRef = Get-CellRef -Row $RowIndex -Column ($i + 1)
        $raw = $Values[$i]
        $numeric = $false
        if ($IsNumber -and $i -lt $IsNumber.Count) {
            $numeric = $IsNumber[$i]
        }

        if ($numeric -and $null -ne $raw -and "$raw" -ne '') {
            $cell = New-NumberCell -Xml $Xml -CellRef $cellRef -Value ([string]$raw)
        } else {
            $cell = New-InlineCell -Xml $Xml -CellRef $cellRef -Value ([string]$raw)
        }

        $null = $row.AppendChild($cell)
    }

    $row
}

function Replace-SheetData {
    param([xml]$Xml, [object[]]$Rows, [bool[][]]$NumberFlags)

    $sheetData = $Xml.SelectSingleNode('/*[local-name()="worksheet"]/*[local-name()="sheetData"]')
    $sheetData.RemoveAll()

    for ($i = 0; $i -lt $Rows.Count; $i++) {
        $rowIndex = $i + 1
        $flags = if ($NumberFlags -and $i -lt $NumberFlags.Count) { $NumberFlags[$i] } else { $null }
        $row = New-SheetRow -Xml $Xml -RowIndex $rowIndex -Values $Rows[$i] -IsNumber $flags
        $null = $sheetData.AppendChild($row)
    }

    $dimension = $Xml.SelectSingleNode('/*[local-name()="worksheet"]/*[local-name()="dimension"]')
    if ($dimension -ne $null) {
        $endRef = Get-CellRef -Row $Rows.Count -Column $Rows[0].Count
        $null = $dimension.SetAttribute('ref', "A1:$endRef")
    }
}

function New-ZipFromDirectoryNormalized {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationZip
    )

    if (Test-Path $DestinationZip) {
        Remove-Item -Path $DestinationZip -Force
    }

    $sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory)
    $zip = [System.IO.Compression.ZipFile]::Open($DestinationZip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = Get-ChildItem -Path $sourceRoot -Recurse -File
        foreach ($file in $files) {
            $relativePath = $file.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
            $entryName = $relativePath -replace '\\', '/'
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    } finally {
        $zip.Dispose()
    }
}

if (-not $workbookPath) {
    throw 'Workbook not found in QA folder.'
}

$implemented = Import-Csv -Path $implementedPath -Delimiter "`t"
$planned = Import-Csv -Path $plannedPath -Delimiter "`t"
$allCases = @($implemented + $planned)

foreach ($case in $allCases) {
    if ([string]::IsNullOrWhiteSpace($case.'P/F')) {
        $case.'P/F' = 'N/T'
    }
}

for ($i = 0; $i -lt $allCases.Count; $i++) {
    $allCases[$i].No = [string]($i + 1)
    if ($allCases[$i].Tag -eq '향후구현') {
        $allCases[$i].비고 = '향후 구현 예정 항목'
    }
}

$caseColumns = @()
foreach ($prop in $allCases[0].PSObject.Properties) {
    $caseColumns += $prop.Name
}

$sheet1Rows = New-Object System.Collections.Generic.List[object[]]
$sheet1Flags = New-Object System.Collections.Generic.List[bool[]]
$sheet1Rows.Add(@($caseColumns))
$sheet1Flags.Add(@($true,$false,$false,$false,$false,$false,$false,$false,$false,$false,$false,$false))

foreach ($case in $allCases) {
    $rowValues = @()
    foreach ($column in $caseColumns) {
        $rowValues += $case.PSObject.Properties[$column].Value
    }
    $sheet1Rows.Add($rowValues)
    $sheet1Flags.Add(@($true,$false,$false,$false,$false,$false,$false,$false,$false,$false,$false,$false))
}

$majorCategoryColumn = $caseColumns[1]
$totalCount = [double]$allCases.Count
$groups = $allCases | Group-Object -Property { $_.PSObject.Properties[$majorCategoryColumn].Value }

$sheet2Rows = New-Object System.Collections.Generic.List[object[]]
$sheet2Flags = New-Object System.Collections.Generic.List[bool[]]
$sheet2Rows.Add(@('TestCase Summary','','','','','','','','',''))
$sheet2Flags.Add(@($false,$false,$false,$false,$false,$false,$false,$false,$false,$false))
$sheet2Rows.Add(@('Category','Count','Ratio','Pass','Fail','N/A','Block','N/T','Total','Total(%)'))
$sheet2Flags.Add(@($false,$false,$false,$false,$false,$false,$false,$false,$false,$false))

$sumPass = 0
$sumFail = 0
$sumNA = 0
$sumBlock = 0
$sumNT = 0
$sumExecuted = 0

foreach ($group in $groups) {
    $count = $group.Count
    $pass = @($group.Group | Where-Object { $_.'P/F' -eq 'Pass' }).Count
    $fail = @($group.Group | Where-Object { $_.'P/F' -eq 'Fail' }).Count
    $na = @($group.Group | Where-Object { $_.'P/F' -eq 'N/A' }).Count
    $block = @($group.Group | Where-Object { $_.'P/F' -eq 'Block' }).Count
    $nt = @($group.Group | Where-Object { $_.'P/F' -eq 'N/T' }).Count
    $executed = $pass + $fail + $na + $block + $nt
    $ratio = if ($totalCount -gt 0) { [math]::Round($count / $totalCount, 6) } else { 0 }
    $executedRatio = if ($count -gt 0) { [math]::Round($executed / $count, 6) } else { 0 }

    $sheet2Rows.Add(@($group.Name,$count,$ratio,$pass,$fail,$na,$block,$nt,$executed,$executedRatio))
    $sheet2Flags.Add(@($false,$true,$true,$true,$true,$true,$true,$true,$true,$true))

    $sumPass += $pass
    $sumFail += $fail
    $sumNA += $na
    $sumBlock += $block
    $sumNT += $nt
    $sumExecuted += $executed
}

$sheet2Rows.Add(@('Total',$allCases.Count,1,$sumPass,$sumFail,$sumNA,$sumBlock,$sumNT,$sumExecuted,''))
$sheet2Flags.Add(@($false,$true,$true,$true,$true,$true,$true,$true,$true,$false))

$sheet2Rows.Add(@(
    'Total(%)',
    '',
    '',
    $(if ($allCases.Count -gt 0) { [math]::Round($sumPass / $allCases.Count, 6) } else { 0 }),
    $(if ($allCases.Count -gt 0) { [math]::Round($sumFail / $allCases.Count, 6) } else { 0 }),
    $(if ($allCases.Count -gt 0) { [math]::Round($sumNA / $allCases.Count, 6) } else { 0 }),
    $(if ($allCases.Count -gt 0) { [math]::Round($sumBlock / $allCases.Count, 6) } else { 0 }),
    $(if ($allCases.Count -gt 0) { [math]::Round($sumNT / $allCases.Count, 6) } else { 0 }),
    $(if ($allCases.Count -gt 0) { [math]::Round($sumExecuted / $allCases.Count, 6) } else { 0 }),
    ''
))
$sheet2Flags.Add(@($false,$false,$false,$true,$true,$true,$true,$true,$true,$false))

Add-Type -AssemblyName System.IO.Compression.FileSystem

$tempDir = Join-Path $env:TEMP ("BackInSchool_QA_" + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($tempDir) | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($workbookPath, $tempDir)

[xml]$sheet1Xml = Get-Content -Path (Join-Path $tempDir 'xl\worksheets\sheet1.xml') -Raw -Encoding UTF8
[xml]$sheet2Xml = Get-Content -Path (Join-Path $tempDir 'xl\worksheets\sheet2.xml') -Raw -Encoding UTF8

Replace-SheetData -Xml $sheet1Xml -Rows $sheet1Rows -NumberFlags $sheet1Flags
Replace-SheetData -Xml $sheet2Xml -Rows $sheet2Rows -NumberFlags $sheet2Flags

$sheet1Xml.Save((Join-Path $tempDir 'xl\worksheets\sheet1.xml'))
$sheet2Xml.Save((Join-Path $tempDir 'xl\worksheets\sheet2.xml'))

$backupPath = $workbookPath + '.bak'
Copy-Item -Path $workbookPath -Destination $backupPath -Force
Remove-Item -Path $workbookPath -Force
New-ZipFromDirectoryNormalized -SourceDirectory $tempDir -DestinationZip $workbookPath
Remove-Item -Path $tempDir -Recurse -Force

Write-Output "Updated workbook: $workbookPath"
Write-Output "Backup created: $backupPath"
