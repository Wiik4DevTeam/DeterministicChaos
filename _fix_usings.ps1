# =============================================================================
# Fix script: Remove misplaced using statements and add missing ones
# =============================================================================

$ErrorActionPreference = "Stop"
$root = "c:\Users\axler\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\DeterministicChaos"

$newUsings = @(
    "using DeterministicChaos.Content.Items.Accessories;",
    "using DeterministicChaos.Content.Items.BossBags;",
    "using DeterministicChaos.Content.Items.BossSummons;",
    "using DeterministicChaos.Content.Items.Consumables;",
    "using DeterministicChaos.Content.Items.DamageClasses;",
    "using DeterministicChaos.Content.Items.Globals;",
    "using DeterministicChaos.Content.Items.Materials;",
    "using DeterministicChaos.Content.Items.Placeable;",
    "using DeterministicChaos.Content.Items.Rarities;",
    "using DeterministicChaos.Content.Items.Weapons;"
)
$usingBlock = ($newUsings -join "`n")

# =============================================================================
# PHASE 1: Remove ALL misplaced using blocks (inside class/method bodies)
# A misplaced block = the 10 using lines appearing AFTER a namespace declaration
# =============================================================================
Write-Host "=== PHASE 1: Removing misplaced using blocks ===" -ForegroundColor Cyan

$allCsFiles = Get-ChildItem -Path $root -Filter "*.cs" -Recurse
$fixCount = 0

foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Pattern: the 10 using lines appearing as a block somewhere in the file
    # We need to find ALL occurrences and remove ones that are NOT at the top of the file
    $lines = $content -split "`n"
    
    # Find the line index of the namespace declaration
    $namespaceLineIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*namespace\s+') {
            $namespaceLineIdx = $i
            break
        }
    }
    
    if ($namespaceLineIdx -eq -1) { continue }
    
    # Find all occurrences of "using DeterministicChaos.Content.Items.Accessories;" AFTER the namespace
    $modified = $false
    $indicesToRemove = @()
    
    for ($i = $namespaceLineIdx; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq "using DeterministicChaos.Content.Items.Accessories;") {
            # Check if the next 9 lines are the rest of our block
            $isBlock = $true
            $blockUsings = @(
                "using DeterministicChaos.Content.Items.Accessories;",
                "using DeterministicChaos.Content.Items.BossBags;",
                "using DeterministicChaos.Content.Items.BossSummons;",
                "using DeterministicChaos.Content.Items.Consumables;",
                "using DeterministicChaos.Content.Items.DamageClasses;",
                "using DeterministicChaos.Content.Items.Globals;",
                "using DeterministicChaos.Content.Items.Materials;",
                "using DeterministicChaos.Content.Items.Placeable;",
                "using DeterministicChaos.Content.Items.Rarities;",
                "using DeterministicChaos.Content.Items.Weapons;"
            )
            
            for ($j = 0; $j -lt 10; $j++) {
                if (($i + $j) -ge $lines.Count -or $lines[$i + $j].Trim() -ne $blockUsings[$j]) {
                    $isBlock = $false
                    break
                }
            }
            
            if ($isBlock) {
                # Mark these 10 lines for removal
                for ($j = 0; $j -lt 10; $j++) {
                    $indicesToRemove += ($i + $j)
                }
                $modified = $true
            }
        }
    }
    
    if ($modified) {
        $newLines = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($indicesToRemove -notcontains $i) {
                $newLines += $lines[$i]
            }
        }
        $content = $newLines -join "`n"
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fixCount++
        Write-Host "  Removed misplaced block from: $($file.Name)" -ForegroundColor Yellow
    }
}

Write-Host "  Fixed $fixCount files." -ForegroundColor Green

# =============================================================================
# PHASE 2: Ensure correct using statements at top of ALL files that need them
# "Need them" = ANY file that references DeterministicChaos.Content.Items
# =============================================================================
Write-Host "=== PHASE 2: Ensuring correct using statements at file tops ===" -ForegroundColor Cyan

# Re-read all files after phase 1 fixes
$allCsFiles = Get-ChildItem -Path $root -Filter "*.cs" -Recurse
$addCount = 0

foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Check if this file references Content.Items in any way
    $needsUsings = $false
    
    # References via using statement
    if ($content -match 'using DeterministicChaos\.Content\.Items;') {
        $needsUsings = $true
    }
    # References via fully-qualified type names
    if ($content -match 'DeterministicChaos\.Content\.Items\.[A-Z]') {
        $needsUsings = $true
    }
    # File IS in Content/Items
    if ($file.FullName -like "*\Content\Items\*") {
        $needsUsings = $true
    }
    
    if (-not $needsUsings) { continue }
    
    # Check which usings are already present
    $usingsToAdd = @()
    foreach ($u in $newUsings) {
        $escapedUsing = [regex]::Escape($u)
        if ($content -notmatch $escapedUsing) {
            $usingsToAdd += $u
        }
    }
    
    if ($usingsToAdd.Count -eq 0) { continue }
    
    $addBlock = ($usingsToAdd -join "`n")
    
    # Find the LAST using statement that is BEFORE the namespace declaration
    $lines = $content -split "`n"
    $namespaceLineIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*namespace\s+') {
            $namespaceLineIdx = $i
            break
        }
    }
    
    if ($namespaceLineIdx -eq -1) { continue }
    
    # Find the last using import line BEFORE the namespace
    $lastUsingIdx = -1
    for ($i = 0; $i -lt $namespaceLineIdx; $i++) {
        if ($lines[$i] -match '^\s*using\s+[A-Za-z]') {
            $lastUsingIdx = $i
        }
    }
    
    if ($lastUsingIdx -ge 0) {
        # Insert after the last using statement before namespace
        $newLines = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $newLines += $lines[$i]
            if ($i -eq $lastUsingIdx) {
                foreach ($u in $usingsToAdd) {
                    $newLines += $u
                }
            }
        }
        $content = $newLines -join "`n"
    }
    else {
        # No using statements exist, insert before namespace
        $newLines = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($i -eq $namespaceLineIdx) {
                foreach ($u in $usingsToAdd) {
                    $newLines += $u
                }
            }
            $newLines += $lines[$i]
        }
        $content = $newLines -join "`n"
    }
    
    Set-Content -Path $file.FullName -Value $content -NoNewline
    $addCount++
}

Write-Host "  Added/fixed usings in $addCount files." -ForegroundColor Green

# =============================================================================
# PHASE 3: Quick check for remaining issues
# =============================================================================
Write-Host "=== PHASE 3: Verification ===" -ForegroundColor Cyan

$issues = 0
$allCsFiles = Get-ChildItem -Path $root -Filter "*.cs" -Recurse
foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    $lines = $content -split "`n"
    
    # Find namespace line
    $namespaceLineIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*namespace\s+') {
            $namespaceLineIdx = $i
            break
        }
    }
    
    if ($namespaceLineIdx -eq -1) { continue }
    
    # Check for any using import statements AFTER the namespace
    for ($i = ($namespaceLineIdx + 1); $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^using DeterministicChaos\.Content\.Items\.(Accessories|BossBags|BossSummons|Consumables|DamageClasses|Globals|Materials|Placeable|Rarities|Weapons);') {
            Write-Host "  WARNING: Still found misplaced using at $($file.Name):$($i+1)" -ForegroundColor Red
            $issues++
            break
        }
    }
}

if ($issues -eq 0) {
    Write-Host "  No misplaced using statements found!" -ForegroundColor Green
} else {
    Write-Host "  Found $issues files still with issues." -ForegroundColor Red
}

Write-Host ""
Write-Host "=== FIX COMPLETE ===" -ForegroundColor Green
