using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Items.Imbued
{
    public class ImbuedEmblemPlayer : ModPlayer
    {
        public bool hasDeterminationEmblem;
        public bool hasIntegrityEmblem;
        public bool hasPatienceEmblem;
        public bool hasPerseveranceEmblem;
        public bool hasKindnessEmblem;
        public bool hasJusticeEmblem;
        public bool hasBraveryEmblem;

        // Flag for Justice Emblem hypercrit VFX (set in ModifyHitNPC, consumed in OnHitNPC)
        public bool justiceMarkHypercritPending;

        public override void ResetEffects()
        {
            hasDeterminationEmblem = false;
            hasIntegrityEmblem = false;
            hasPatienceEmblem = false;
            hasPerseveranceEmblem = false;
            hasKindnessEmblem = false;
            hasJusticeEmblem = false;
            hasBraveryEmblem = false;
        }
    }
}
