# Read file content
$content = Get-Content -Path Form1.cs -Raw

# Find start of first AnalyzeTrend method
$methodStart1 = $content.IndexOf("private string AnalyzeTrend")
if ($methodStart1 -eq -1) {
    Write-Error "AnalyzeTrend method not found"
    exit 1
}

# Find end of first method (matching braces)
$openBraces = 0
$methodEnd1 = -1
for ($i = $methodStart1; $i -lt $content.Length; $i++) {
    if ($content[$i] -eq '{') {
        $openBraces++
    } elseif ($content[$i] -eq '}') {
        $openBraces--
        if ($openBraces -eq 0) {
            $methodEnd1 = $i
            break
        }
    }
}

if ($methodEnd1 -eq -1) {
    Write-Error "Could not find end of first AnalyzeTrend method"
    exit 1
}

# Find start of second AnalyzeTrend method
$methodStart2 = $content.IndexOf("private string AnalyzeTrend", $methodEnd1)
if ($methodStart2 -eq -1) {
    Write-Host "Only one AnalyzeTrend method found, no fix needed"
    exit 0
}

# Find all remaining method end positions
$openBraces = 0
$lastMethodEnd = -1
for ($i = $methodStart2; $i -lt $content.Length; $i++) {
    if ($content[$i] -eq '{') {
        $openBraces++
    } elseif ($content[$i] -eq '}') {
        $openBraces--
        if ($openBraces -eq 0) {
            $lastMethodEnd = $i
        }
    }
}

if ($lastMethodEnd -eq -1) {
    Write-Error "Could not find end of last method"
    exit 1
}

# Build new content: keep first method, remove duplicates
$newContent = $content.Substring(0, $methodEnd1 + 1) + $content.Substring($lastMethodEnd + 1)

# Write fixed file
Set-Content -Path Form1.cs -Value $newContent

Write-Host "Successfully fixed duplicate AnalyzeTrend methods"
