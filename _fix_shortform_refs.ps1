# =============================================================================
# Fix script: Replace Items.XXX short-form references with bare type names
# Since the using imports (DeterministicChaos.Content.Items.Weapons, etc.)
# are already present, bare type names will resolve correctly.
# =============================================================================

$ErrorActionPreference = "Stop"
$root = "c:\Users\axler\OneDrive\Documents\My Games\Terraria\tModLoader\ModSources\DeterministicChaos"

# All type names that were moved from Content.Items root to subfolders
$movedTypes = @(
    # Accessories
    "DeckOfCards", "DeckOfCardsPlayer", "EmptyEmblem", "OopsAllCrits",
    "RoaringLens", "RoaringLensPlayer", "RoaringRing",
    "RoaringShield", "RoaringShieldPlayer", "ShadowMantle",
    "TitanicEmblem", "TitanicEmblemPlayer", "DarkShard", "DarkShardPlayer",
    "TitanStar", "TitanStarPlayer",
    # BossBags
    "JevilBossBag", "KnightBossBag", "TitanBossBag",
    # BossSummons
    "DevilsKey", "ERAMSummon", "SuspiciousEye",
    # Consumables
    "Soulflicker", "TitansBaneFlask", "TitansBaneImbuePlayer",
    # DamageClasses
    "MagicRogueDamageClass", "MeleeMagicDamageClass", "RangedMagicDamageClass",
    "RangedMeleeDamageClass", "RangedSummonDamageClass", "SummonerMeleeDamageClass",
    # Globals
    "CalamityBossLootHelper", "HasSoulTraitCondition", "FryingPanGlobalTile",
    "BagDropCondition", "DirectDropCondition",
    # Materials
    "DarkFragment", "RoaringArrow", "RoaringBullet", "SoulCatalyst",
    "TitansArrow", "Titansblood",
    # Placeable
    "TitanForgeItem",
    # Rarities
    "DarkWorldRarity", "SoulTraitRarity", "SoulTraitRarityGlobalItem",
    "TitanRarity", "TitanRarityGlobalItem",
    # Weapons (all)
    "AceOfSpades", "AerodynamicBoots", "AerodynamicBootsPlayer",
    "Appendage", "BalletShoes", "BalletShoesPlayer", "BaseballBat",
    "Cascade", "CookingPot", "CookingPotPlayer", "Devilsknife",
    "ForthcomingWrath", "ForthcomingWrathPlayer", "FryingPan", "FryingPanPlayer",
    "HollowGun", "HollowGunPlayer", "Incandescent", "IncandescentPlayer",
    "JackOfClubs", "JusticeDecree", "JusticeDecreePlayer", "KingOfHearts",
    "Leyline", "LodestoneFork", "QueenOfDiamonds",
    "RealKnife", "RealKnifePlayer", "RoaringBow", "RoaringGun",
    "RoaringSummon", "RoaringSword", "RoaringSwordPlayer",
    "RoaringTome", "RoaringTomePlayer", "RoaringWhip", "RoaringWhipPlayer",
    "RoaringYoyo", "RoaringYoyoPlayer", "RodOfStagnation",
    "RustyKnife", "RustyKnifePlayer", "ShatteredGlass",
    "SpecialistsNotebook", "SpecialistsNotebookPlayer", "SpecialistsNotebookUI",
    "SteadfastV1", "SteadfastV1Player",
    "TheAbstract", "TheAbstractPlayer", "TheAbstractUI",
    "TornNotebook", "TornNotebookPlayer", "TornNotebookUI",
    "ToughGauntlet", "ToughGauntletPlayer", "ToughGlove", "ToughGlovePlayer",
    "ToyerKnife", "ToyerKnifePlayer", "ToyKnife", "ToyKnifePlayer",
    "TrueKnife", "TrueKnifePlayer"
)

# Also include PickTwoDropRule which is defined inside TitanBossBag.cs (now in BossBags namespace)
$movedTypes += "PickTwoDropRule"

Write-Host "=== Fixing Items.XXX short-form references ===" -ForegroundColor Cyan

$allCsFiles = Get-ChildItem -Path $root -Filter "*.cs" -Recurse
$fixCount = 0

foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false
    
    foreach ($typeName in $movedTypes) {
        # Match Items.TypeName that is NOT:
        # - Part of a using statement (starts with "using ")
        # - Part of a namespace declaration
        # - Already in a sub-path like Items.Weapons.TypeName
        
        # Pattern: "Items.TypeName" NOT preceded by a dot (which would mean it's a deeper path)
        # and NOT in a using statement
        $pattern = '(?<![\.\w])Items\.' + [regex]::Escape($typeName) + '(?![\w\.])'
        
        if ($content -match $pattern) {
            # Check this isn't inside a using statement line
            $lines = $content -split "`n"
            $needsFix = $false
            foreach ($line in $lines) {
                if ($line -match $pattern -and $line -notmatch '^\s*using\s+') {
                    $needsFix = $true
                    break
                }
            }
            
            if ($needsFix) {
                # Replace Items.TypeName with just TypeName, but NOT in using statements
                $newLines = @()
                foreach ($line in $lines) {
                    if ($line -match '^\s*using\s+') {
                        $newLines += $line
                    }
                    else {
                        $newLines += ($line -replace $pattern, $typeName)
                    }
                }
                $content = $newLines -join "`n"
                $modified = $true
            }
        }
    }
    
    if ($modified) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fixCount++
        Write-Host "  Fixed short-form refs in: $($file.Name)" -ForegroundColor Yellow
    }
}

Write-Host "  Fixed $fixCount files." -ForegroundColor Green

# =============================================================================
# PHASE 2: Ensure all affected files have the sub-namespace usings
# =============================================================================
Write-Host "=== Ensuring usings in files that were just fixed ===" -ForegroundColor Cyan

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

# Re-read all files
$allCsFiles = Get-ChildItem -Path $root -Filter "*.cs" -Recurse
$addCount = 0

foreach ($file in $allCsFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Check if this file references any moved type
    $needsUsings = $false
    foreach ($typeName in $movedTypes) {
        if ($content -match "(?<![\.\w])$typeName(?![\w])") {
            $needsUsings = $true
            break
        }
    }
    if ($content -match 'DeterministicChaos\.Content\.Items\.') {
        $needsUsings = $true
    }
    if ($file.FullName -like "*\Content\Items\*") {
        $needsUsings = $true
    }
    
    if (-not $needsUsings) { continue }
    
    # Check which usings are missing
    $usingsToAdd = @()
    foreach ($u in $newUsings) {
        if ($content -notmatch [regex]::Escape($u)) {
            $usingsToAdd += $u
        }
    }
    
    if ($usingsToAdd.Count -eq 0) { continue }
    
    # Find the namespace line
    $lines = $content -split "`n"
    $namespaceLineIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*namespace\s+') {
            $namespaceLineIdx = $i
            break
        }
    }
    if ($namespaceLineIdx -eq -1) { continue }
    
    # Find last using before namespace
    $lastUsingIdx = -1
    for ($i = 0; $i -lt $namespaceLineIdx; $i++) {
        if ($lines[$i] -match '^\s*using\s+[A-Za-z]') {
            $lastUsingIdx = $i
        }
    }
    
    if ($lastUsingIdx -ge 0) {
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

Write-Host "  Added usings to $addCount files." -ForegroundColor Green

Write-Host ""
Write-Host "=== FIX COMPLETE ===" -ForegroundColor Green
