using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using ReLogic.Content;
using DeterministicChaos.Content.VFX;
using System;
using System.IO;
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

namespace DeterministicChaos.Content.NPCs.Bosses
{
    // A star centered on TitanBody's face (slightly above hitbox center).
    // Shakes when a multiplier orb lands. Hidden during the Damage phase.
    // Sprite degrades through TitanStarD1-D4 at multiplier milestones (1.5, 2.0, 2.5, 3.0).
    // Reverts to TitanStar when parkour begins again.
    public class TitanStar : ModNPC
    {
        // ai[0] = parent TitanBody whoAmI
        private const float FACE_OFFSET_Y = -20f; // Pixels above TitanBody center (face position)

        // Shake system
        private const float SHAKE_DURATION = 20f;          // Normal shake ticks
        private const float SHAKE_INTENSITY = 3f;           // Normal max pixel offset
        private const float HEAVY_SHAKE_DURATION = 30f;     // Heavier shake on milestone
        private const float HEAVY_SHAKE_INTENSITY = 6f;     // Heavier pixel offset
        private float shakeTimer = 0f;
        private float currentShakeIntensity = SHAKE_INTENSITY;
        private float currentShakeDuration = SHAKE_DURATION;
        private Vector2 shakeOffset = Vector2.Zero;

        // Cutscene shake
        private const float CUTSCENE_SHAKE_INTERVAL = 40f;  // Ticks between cutscene shakes
        private float cutsceneShakeTimer = 0f;

        // Visibility
        private bool visible = true;
        private bool wasVisible = true;
        private TitanBody.FightPhase previousPhase = TitanBody.FightPhase.Parkour;

        // Reverse cycle animation in last 0.8s of Damage phase
        private const float REVERSE_CYCLE_DURATION = 0.8f;
        private const float REVERSE_STAGE_INTERVAL = 0.2f; // 4 stages × 0.2s = 0.8s

        // Damage stage tracking (0 = base, 1-4 = D1-D4)
        private int damageStage = 0;
        private static Asset<Texture2D>[] damageTextures;

        // ── Multiplayer sync ──────────────────────────────────────────
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(shakeTimer);
            writer.Write(damageStage);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            shakeTimer = reader.ReadSingle();
            damageStage = reader.ReadInt32();
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 1;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
        }

        public override void AI()
        {
            int parentIdx = (int)NPC.ai[0];

            // Despawn if parent TitanBody is gone
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs
                || !Main.npc[parentIdx].active
                || Main.npc[parentIdx].type != ModContent.NPCType<TitanBody>())
            {
                NPC.active = false;
                return;
            }

            NPC parent = Main.npc[parentIdx];

            if (parent.ModNPC is TitanBody titan)
            {
                var currentPhase = titan.CurrentPhase;

                // Determine visibility: hidden during Damage phase EXCEPT last 0.8s
                if (currentPhase == TitanBody.FightPhase.Damage)
                {
                    float timeLeft = TitanBody.DAMAGE_DURATION - titan.PhaseTimer;
                    if (timeLeft <= REVERSE_CYCLE_DURATION)
                    {
                        visible = true;

                        // Cycle through D4 → D3 → D2 → D1 over 0.8s (0.2s each)
                        float elapsed = REVERSE_CYCLE_DURATION - timeLeft; // 0.0 → 0.8
                        int reverseStage = Math.Min((int)(elapsed / REVERSE_STAGE_INTERVAL), 3); // 0,1,2,3
                        damageStage = 4 - reverseStage; // 4 → 3 → 2 → 1
                    }
                    else
                    {
                        visible = false;
                    }
                }
                else
                {
                    visible = true;
                }

                // ── Transition: visible → hidden (entering Damage phase) ──
                if (wasVisible && !visible)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/GlassShatter") { MaxInstances = 1 });

                    // Burst shard particles outward in cardinal directions
                    StarShardParticle.SpawnOutwardBurst(NPC.GetSource_FromAI(), NPC.Center);
                }

                // ── Revert sprite when returning to Parkour ──
                if (currentPhase == TitanBody.FightPhase.Parkour && previousPhase != TitanBody.FightPhase.Parkour)
                {
                    damageStage = 0;
                    NPC.netUpdate = true;
                }

                // ── Track damage multiplier milestones ──
                int targetStage = GetDamageStage(titan.DamageMultiplier);
                if (targetStage > damageStage)
                {
                    damageStage = targetStage;
                    NPC.netUpdate = true;

                    // Play GlassCracking and trigger heavy shake on milestone
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/GlassCracking") { MaxInstances = 1 });
                    TriggerHeavyShake();
                }

                // ── Cutscene shakes ──
                if (currentPhase == TitanBody.FightPhase.Cutscene)
                {
                    cutsceneShakeTimer += 1f;
                    if (cutsceneShakeTimer >= CUTSCENE_SHAKE_INTERVAL)
                    {
                        cutsceneShakeTimer = 0f;
                        TriggerShake();
                    }
                }
                else
                {
                    cutsceneShakeTimer = 0f;
                }

                wasVisible = visible;
                previousPhase = currentPhase;
            }

            // Follow TitanBody's face (slightly above center)
            NPC.Center = parent.Center + new Vector2(0f, FACE_OFFSET_Y);

            // Apply shake offset
            if (shakeTimer > 0f)
            {
                shakeTimer--;
                float intensity = currentShakeIntensity * (shakeTimer / currentShakeDuration);
                shakeOffset = new Vector2(
                    Main.rand.NextFloat(-intensity, intensity),
                    Main.rand.NextFloat(-intensity, intensity));
            }
            else
            {
                shakeOffset = Vector2.Zero;
            }

            NPC.rotation = 0f;
            NPC.velocity = Vector2.Zero;
        }

        // Returns the damage stage (0-4) based on multiplier milestones at 1.5, 2.0, 2.5, 3.0.
        private static int GetDamageStage(float multiplier)
        {
            if (multiplier >= 3.0f) return 4;
            if (multiplier >= 2.5f) return 3;
            if (multiplier >= 2.0f) return 2;
            if (multiplier >= 1.5f) return 1;
            return 0;
        }

        // Triggers a normal-intensity shake (called when a multiplier orb hits).
        public void TriggerShake()
        {
            shakeTimer = SHAKE_DURATION;
            currentShakeDuration = SHAKE_DURATION;
            currentShakeIntensity = SHAKE_INTENSITY;
            NPC.netUpdate = true;
        }

        // Triggers a heavier shake (called on damage stage milestone).
        public void TriggerHeavyShake()
        {
            shakeTimer = HEAVY_SHAKE_DURATION;
            currentShakeDuration = HEAVY_SHAKE_DURATION;
            currentShakeIntensity = HEAVY_SHAKE_INTENSITY;
            NPC.netUpdate = true;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!visible)
                return false;

            // Load damage textures on first draw
            if (damageTextures == null)
            {
                damageTextures = new Asset<Texture2D>[4];
                for (int i = 0; i < 4; i++)
                    damageTextures[i] = ModContent.Request<Texture2D>(
                        $"DeterministicChaos/Content/NPCs/Bosses/TitanStarD{i + 1}", AssetRequestMode.ImmediateLoad);
            }

            Texture2D tex;
            if (damageStage >= 1 && damageStage <= 4)
                tex = damageTextures[damageStage - 1].Value;
            else
                tex = Terraria.GameContent.TextureAssets.Npc[Type].Value;

            Vector2 drawPos = NPC.Center + shakeOffset - screenPos;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            spriteBatch.Draw(tex, drawPos, null, drawColor, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool CheckActive() => false;
    }
}
