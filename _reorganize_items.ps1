# =============================================================================
# DeterministicChaos Items Folder Reorganization Script
# Moves root-level Content/Items/ files into categorized subfolders
# and updates namespaces, using statements, and texture path references.
# =============================================================================

$ErrorActionPreference = "Stop"
$root = "c:\Users\axler\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\DeterministicChaos"
$itemsDir = "$root\Content\Items"

# =============================================================================
# PHASE 1: Define file-to-folder mapping (base names, without extension)
# =============================================================================
Write-Host "=== PHASE 1: Defining file mappings ===" -ForegroundColor Cyan

$categoryMap = @{
    "Accessories" = @(
        "DeckOfCards", "DeckOfCardsPlayer",
        "EmptyEmblem",
        "OopsAllCrits",
        "RoaringLens", "RoaringLensPlayer",
        "RoaringRing",
        "RoaringShield", "RoaringShieldPlayer",
        "ShadowMantle",
        "TitanicEmblem", "TitanicEmblemPlayer",
        "DarkShard", "DarkShardPlayer",
        "TitanStar", "TitanStarPlayer"
    )
    "BossBags" = @(
        "JevilBossBag",
        "KnightBossBag",
        "TitanBossBag"
    )
    "BossSummons" = @(
        "DevilsKey",
        "ERAMSummon",
        "SuspiciousEye"
    )
    "Consumables" = @(
        "Soulflicker",
        "TitansBaneFlask",
        "TitansBaneImbuePlayer"
    )
    "DamageClasses" = @(
        "MagicRogueDamageClass",
        "MeleeMagicDamageClass",
        "RangedMagicDamageClass",
        "RangedMeleeDamageClass",
        "RangedSummonDamageClass",
        "SummonerMeleeDamageClass"
    )
    "Globals" = @(
        "CalamityBossLootHelper",
        "HasSoulTraitCondition",
        "FryingPanGlobalTile"
    )
    "Materials" = @(
        "DarkFragment",
        "RoaringArrow",
        "RoaringBullet",
        "SoulCatalyst",
        "TitansArrow",
        "Titansblood"
    )
    "Placeable" = @(
        "TitanForgeItem"
    )
    "Rarities" = @(
        "DarkWorldRarity",
        "SoulTraitRarity",
        "SoulTraitRarityGlobalItem",
        "TitanRarity",
        "TitanRarityGlobalItem"
    )
    "Weapons" = @(
        "AceOfSpades",
        "AerodynamicBoots", "AerodynamicBootsPlayer",
        "Appendage",
        "BalletShoes", "BalletShoesPlayer",
        "BaseballBat",
        "Cascade",
        "CookingPot", "CookingPotPlayer",
        "Devilsknife",
        "ForthcomingWrath", "ForthcomingWrathPlayer",
        "FryingPan", "FryingPanPlayer",
        "HollowGun", "HollowGunPlayer",
        "Incandescent", "IncandescentPlayer",
        "JackOfClubs",
        "JusticeDecree", "JusticeDecreePlayer",
        "KingOfHearts",
        "Leyline",
        "LodestoneFork",
        "QueenOfDiamonds",
        "RealKnife", "RealKnifePlayer",
        "RoaringBow",
        "RoaringGun",
        "RoaringSummon",
        "RoaringSword", "RoaringSwordPlayer",
        "RoaringTome", "RoaringTomePlayer",
        "RoaringWhip", "RoaringWhipPlayer",
        "RoaringYoyo", "RoaringYoyoPlayer",
        "RodOfStagnation",
        "RustyKnife", "RustyKnifePlayer",
        "ShatteredGlass",
        "SpecialistsNotebook", "SpecialistsNotebookPlayer", "SpecialistsNotebookUI",
        "SteadfastV1", "SteadfastV1Player",
        "TheAbstract", "TheAbstractPlayer", "TheAbstractUI",
        "TornNotebook", "TornNotebookPlayer", "TornNotebookUI",
        "ToughGauntlet", "ToughGauntletPlayer",
        "ToughGlove", "ToughGlovePlayer",
        "ToyerKnife", "ToyerKnifePlayer",
        "ToyKnife", "ToyKnifePlayer",
        "TrueKnife", "TrueKnifePlayer"
    )
}

# Extra .png files that don't match a base name exactly
$extraPngMoves = @{
    "Accessories" = @("ShadowMantle_Arms.png", "ShadowMantle_Back.png", "ShadowMantle_Neck.png")
    "Placeable"   = @("TitanForge.png")
    "Weapons"     = @("AttackPrompt.png")
}

# =============================================================================
# PHASE 2: Create directories
# =============================================================================
Write-Host "=== PHASE 2: Creating directories ===" -ForegroundColor Cyan

foreach ($folder in $categoryMap.Keys) {
    $path = Join-Path $itemsDir $folder
    if (-not (Test-Path $path)) {
        New-Item -Path $path -ItemType Directory -Force | Out-Null
        Write-Host "  Created: $folder/"
    }
}

# =============================================================================
# PHASE 3: Move files
# =============================================================================
Write-Host "=== PHASE 3: Moving files ===" -ForegroundColor Cyan

$moveCount = 0

foreach ($folder in $categoryMap.Keys) {
    $destDir = Join-Path $itemsDir $folder
    foreach ($baseName in $categoryMap[$folder]) {
        # Move .cs file
        $csFile = Join-Path $itemsDir "$baseName.cs"
        if (Test-Path $csFile) {
            Move-Item -Path $csFile -Destination (Join-Path $destDir "$baseName.cs")
            $moveCount++
        }
        # Move matching .png file
        $pngFile = Join-Path $itemsDir "$baseName.png"
        if (Test-Path $pngFile) {
            Move-Item -Path $pngFile -Destination (Join-Path $destDir "$baseName.png")
            $moveCount++
        }
    }
    # Move extra png files
    if ($extraPngMoves.ContainsKey($folder)) {
        foreach ($png in $extraPngMoves[$folder]) {
            $pngFile = Join-Path $itemsDir $png
            if (Test-Path $pngFile) {
                Move-Item -Path $pngFile -Destination (Join-Path $destDir $png)
                $moveCount++
            }
        }
    }
}

Write-Host "  Moved $moveCount files total." -ForegroundColor Green

# =============================================================================
# PHASE 4: Update namespaces in moved .cs files
# =============================================================================
Write-Host "=== PHASE 4: Updating namespaces in moved files ===" -ForegroundColor Cyan

$nsUpdateCount = 0

foreach ($folder in $categoryMap.Keys) {
    $destDir = Join-Path $itemsDir $folder
    foreach ($baseName in $categoryMap[$folder]) {
        $csFile = Join-Path $destDir "$baseName.cs"
        if (Test-Path $csFile) {
            $content = Get-Content $csFile -Raw
            # Replace "namespace DeterministicChaos.Content.Items" when NOT already followed by a dot
            # This handles both brace-style and file-scoped namespaces
            $oldNs = "namespace DeterministicChaos.Content.Items"
            $newNs = "namespace DeterministicChaos.Content.Items.$folder"
            
            # Match "namespace DeterministicChaos.Content.Items" followed by whitespace or { or ;
            # but NOT followed by .Something (to avoid double-modifying)
            $pattern = 'namespace DeterministicChaos\.Content\.Items(?!\.)'
            if ($content -match $pattern) {
                $content = $content -replace $pattern, "namespace DeterministicChaos.Content.Items.$folder"
                Set-Content -Path $csFile -Value $content -NoNewline
                $nsUpdateCount++
            }
        }
    }
}

Write-Host "  Updated namespaces in $nsUpdateCount files." -ForegroundColor Green

# =============================================================================
# PHASE 5: Add using statements to all affected .cs files
# =============================================================================
Write-Host "=== PHASE 5: Adding using statements ===" -ForegroundColor Cyan

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
$newUsingsBlock = ($newUsings -join "`n")

$usingUpdateCount = 0

# Find all .cs files in the project
$allCsFiles = Get-ChildItem -Path $root -Filter "*.cs" -Recurse

foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    $needsUpdate = $false
    
    # Check if file references Content.Items namespace
    if ($content -match 'using DeterministicChaos\.Content\.Items;') {
        $needsUpdate = $true
    }
    # Check if file IS in Content/Items/ (any subfolder)
    if ($file.FullName -like "*\Content\Items\*") {
        $needsUpdate = $true
    }
    
    if ($needsUpdate) {
        # Don't add usings that are already present
        $usingsToAdd = @()
        foreach ($u in $newUsings) {
            $escapedUsing = [regex]::Escape($u)
            if ($content -notmatch $escapedUsing) {
                $usingsToAdd += $u
            }
        }
        
        if ($usingsToAdd.Count -gt 0) {
            $addBlock = ($usingsToAdd -join "`n")
            
            if ($content -match 'using DeterministicChaos\.Content\.Items;') {
                # Insert after the existing using line
                $content = $content -replace '(using DeterministicChaos\.Content\.Items;)', "`$1`n$addBlock"
            }
            elseif ($content -match '(using [^;]+;)') {
                # Insert after the last using statement
                $lastUsingMatch = [regex]::Matches($content, 'using [^;]+;')
                if ($lastUsingMatch.Count -gt 0) {
                    $lastMatch = $lastUsingMatch[$lastUsingMatch.Count - 1]
                    $insertPos = $lastMatch.Index + $lastMatch.Length
                    $content = $content.Insert($insertPos, "`n$addBlock")
                }
            }
            
            Set-Content -Path $file.FullName -Value $content -NoNewline
            $usingUpdateCount++
        }
    }
}

Write-Host "  Added using statements to $usingUpdateCount files." -ForegroundColor Green

# =============================================================================
# PHASE 6: Fix hardcoded texture path strings
# =============================================================================
Write-Host "=== PHASE 6: Fixing texture path references ===" -ForegroundColor Cyan

# Build a lookup: item base name -> new subfolder
$itemToFolder = @{}
foreach ($folder in $categoryMap.Keys) {
    foreach ($baseName in $categoryMap[$folder]) {
        $itemToFolder[$baseName] = $folder
    }
}

# Also map the extra png base names (without extension)
$itemToFolder["ShadowMantle_Arms"] = "Accessories"
$itemToFolder["ShadowMantle_Back"] = "Accessories"
$itemToFolder["ShadowMantle_Neck"] = "Accessories"
$itemToFolder["TitanForge"] = "Placeable"
$itemToFolder["AttackPrompt"] = "Weapons"

$texFixCount = 0

foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false
    
    # Find all references like "DeterministicChaos/Content/Items/SomeName"
    # where SomeName is NOT already in a subfolder (no additional /)
    $matches = [regex]::Matches($content, '"DeterministicChaos/Content/Items/([A-Za-z0-9_]+)"')
    
    foreach ($m in $matches) {
        $itemName = $m.Groups[1].Value
        $fullMatch = $m.Value
        
        if ($itemToFolder.ContainsKey($itemName)) {
            $newFolder = $itemToFolder[$itemName]
            $newPath = "`"DeterministicChaos/Content/Items/$newFolder/$itemName`""
            $content = $content.Replace($fullMatch, $newPath)
            $modified = $true
        }
    }
    
    if ($modified) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $texFixCount++
    }
}

Write-Host "  Fixed texture paths in $texFixCount files." -ForegroundColor Green

# =============================================================================
# PHASE 7: Verify no files left at root (except existing subfolders)
# =============================================================================
Write-Host "=== PHASE 7: Verification ===" -ForegroundColor Cyan

$remainingFiles = Get-ChildItem -Path $itemsDir -File
if ($remainingFiles.Count -eq 0) {
    Write-Host "  Content/Items/ root is clean - all files moved to subfolders!" -ForegroundColor Green
} else {
    Write-Host "  WARNING: These files remain at Content/Items/ root:" -ForegroundColor Yellow
    foreach ($f in $remainingFiles) {
        Write-Host "    - $($f.Name)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== REORGANIZATION COMPLETE ===" -ForegroundColor Green
Write-Host "Summary:"
Write-Host "  - Files moved: $moveCount"
Write-Host "  - Namespaces updated: $nsUpdateCount"
Write-Host "  - Files with added usings: $usingUpdateCount"
Write-Host "  - Texture paths fixed: $texFixCount"
Write-Host ""
Write-Host "Next step: Build the mod to verify no compile errors." -ForegroundColor Yellow
