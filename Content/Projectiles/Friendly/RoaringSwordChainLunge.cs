using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items;
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
using DeterministicChaos.Content.Items.Armor;
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.Items.Prefixes;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSwordChainLunge : ModProjectile
    {
        private const int LungeDuration = 12;
        private const float LungeSpeed = 90f;
        private const float ChainRadius = 1200f;
        private const int MaxAfterimages = 8;
        
        private ref float Timer => ref Projectile.ai[0];
        private ref float TargetNPC => ref Projectile.ai[1];
        
        private Vector2 lungeDirection;
        private Vector2 startPosition;
        private bool hitTarget = false;
        private bool slashSpawned = false;
        private bool initialized = false;
        private bool healedAlly = false;
        
        // Track NPCs already hit in this chain to prevent double-targeting
        private static HashSet<int> chainHitNPCs = new HashSet<int>();
        
        private List<Vector2> afterimagePositions = new List<Vector2>();
        private List<float> afterimageRotations = new List<float>();

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(lungeDirection);
            writer.WriteVector2(startPosition);
            writer.Write(hitTarget);
            writer.Write(slashSpawned);
            writer.Write(initialized);
            writer.Write(healedAlly);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            lungeDirection = reader.ReadVector2();
            startPosition = reader.ReadVector2();
            hitTarget = reader.ReadBoolean();
            slashSpawned = reader.ReadBoolean();
            initialized = reader.ReadBoolean();
            healedAlly = reader.ReadBoolean();
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LungeDuration + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            // Initialize on first frame
            if (!initialized)
            {
                initialized = true;
                lungeDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * player.direction);
                startPosition = player.Center;
                
                // If this is the first lunge in a chain (Timer starts at 0 and no target specified), clear the hit list
                if (TargetNPC <= 0)
                {
                    chainHitNPCs.Clear();
                }
                
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, player.Center);
                
                player.immune = true;
                player.immuneTime = LungeDuration;
                
                // Sync initial state in multiplayer
                Projectile.netUpdate = true;
            }
            
            // Movement during lunge
            if (Timer < LungeDuration)
            {
                // Store afterimage before movement
                afterimagePositions.Insert(0, player.Center);
                afterimageRotations.Insert(0, Projectile.rotation);
                
                if (afterimagePositions.Count > MaxAfterimages)
                {
                    afterimagePositions.RemoveAt(afterimagePositions.Count - 1);
                    afterimageRotations.RemoveAt(afterimageRotations.Count - 1);
                }
                
                float progress = Timer / LungeDuration;
                float easedSpeed = LungeSpeed * (1f - EaseOutQuad(progress));
                
                player.velocity = lungeDirection * easedSpeed;
                player.direction = lungeDirection.X >= 0 ? 1 : -1;
                
                Projectile.Center = player.Center;
                Projectile.rotation = lungeDirection.ToRotation();
                
                for (int i = 0; i < 3; i++)
                {
                    Vector2 dustPos = player.Center + Main.rand.NextVector2Circular(20f, 20f);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.Shadowflame, -player.velocity.X * 0.2f, -player.velocity.Y * 0.2f, 100, default, 1.5f);
                    dust.noGravity = true;
                }
                
                Dust whiteDust = Dust.NewDustPerfect(player.Center, DustID.WhiteTorch, -lungeDirection * 2f, 0, Color.White, 1.2f);
                whiteDust.noGravity = true;
                
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
                
                if (Timer >= LungeDuration * 0.3f && !slashSpawned)
                {
                    slashSpawned = true;
                    // Don't spawn chain slash when targeting an ally
                    if (TargetNPC < 1000)
                        SpawnChainSlash(player);
                }

                // Kindness: Check proximity to ally target for healing
                if (TargetNPC >= 1000 && !healedAlly)
                {
                    int allyIdx = (int)TargetNPC - 1000;
                    if (allyIdx >= 0 && allyIdx < Main.maxPlayers)
                    {
                        Player ally = Main.player[allyIdx];
                        if (ally != null && ally.active && !ally.dead &&
                            Vector2.Distance(player.Center, ally.Center) < 60f)
                        {
                            healedAlly = true;
                            hitTarget = true;

                            int weaponDamage = Projectile.damage;
                            int healAmount = Math.Max(1, (int)(weaponDamage * 0.10f));

                            var prefixPlayer = player.GetModPlayer<PrefixEffectPlayer>();
                            healAmount = prefixPlayer.ScaleHeal(healAmount);

                            var emblemPlayer = player.GetModPlayer<ImbuedEmblemPlayer>();
                            if (emblemPlayer.hasKindnessEmblem)
                                healAmount = (int)(healAmount * 1.25f);

                            if (healAmount > 0 && ally.statLife < ally.statLifeMax2)
                            {
                                ally.statLife = Math.Min(ally.statLife + healAmount, ally.statLifeMax2);
                                ally.HealEffect(healAmount);
                            }

                            RoaringGunPlayer.NotifyAllyHealed(Projectile.owner);

                            // Green healing dust burst
                            for (int d = 0; d < 12; d++)
                            {
                                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                                Dust dust = Dust.NewDustPerfect(ally.Center, DustID.GreenTorch, vel, 0, default, 1.5f);
                                dust.noGravity = true;
                            }
                        }
                    }
                }
            }
            
            if (Timer == LungeDuration)
            {
                player.velocity *= 0.2f;
            }
            
            Lighting.AddLight(Projectile.Center, 1f, 1f, 1f);
            
            Timer++;
            if (Timer >= LungeDuration + 5)
            {
                if (hitTarget && Projectile.owner == Main.myPlayer)
                {
                    ChainToNextTarget(player);
                }
                else
                {
                    // Start cooldown only if not chaining to another target
                    player.GetModPlayer<RoaringSwordPlayer>().StartLungeCooldown();
                }
                Projectile.Kill();
            }
        }
        
        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
        
        private void SpawnChainSlash(Player player)
        {
            if (Projectile.owner != Main.myPlayer)
                return;
                
            float slashAngle = lungeDirection.ToRotation();
            Vector2 slashPosition = player.Center + lungeDirection * 50f;
            
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                slashPosition,
                Vector2.Zero,
                ModContent.ProjectileType<RoaringSwordChainSlash>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                slashAngle
            );
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target == null || !target.active)
                return;
                
            hitTarget = true;
            
            // Add this NPC to the hit list so we don't target it again
            chainHitNPCs.Add(target.whoAmI);
            
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            markNPC.ClearMarks(target);
        }
        
        private void ChainToNextTarget(Player player)
        {
            if (player == null || !player.active || player.dead)
                return;

            var swordPlayer = player.GetModPlayer<RoaringSwordPlayer>();
            int maxMarks = swordPlayer.willbreakerMaxMarks;
                
            float closestDist = ChainRadius;
            NPC closestTarget = null;
            
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;
                
                if (chainHitNPCs.Contains(npc.whoAmI))
                    continue;
                
                RoaringSwordMarkGlobalNPC markNPC = npc.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
                if (markNPC.markStacks < maxMarks)
                    continue;
                
                if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                    continue;
                    
                float dist = Vector2.Distance(player.Center, npc.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestTarget = npc;
                }
            }
            
            if (closestTarget != null && closestTarget.active)
            {
                player.itemAnimation = 0;
                player.itemTime = 0;
                
                Vector2 direction = (closestTarget.Center - player.Center).SafeNormalize(Vector2.UnitX);
                
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    player.Center,
                    direction,
                    ModContent.ProjectileType<RoaringSwordChainLunge>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    0f,
                    closestTarget.whoAmI
                );
            }
            else if (swordPlayer.imbuedWillbreakerVariant == (int)ImbuedWillbreakerVariant.Kindness)
            {
                // Kindness: After all enemies, lunge to an ally to heal them
                float closestAllyDist = ChainRadius;
                int closestAllyIdx = -1;

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (i == Projectile.owner) continue;
                    Player ally = Main.player[i];
                    if (!ally.active || ally.dead || ally.team != player.team || player.team == 0)
                        continue;
                    if (ally.statLife >= ally.statLifeMax2)
                        continue;

                    float dist = Vector2.Distance(player.Center, ally.Center);
                    if (dist < closestAllyDist)
                    {
                        closestAllyDist = dist;
                        closestAllyIdx = i;
                    }
                }

                if (closestAllyIdx >= 0)
                {
                    player.itemAnimation = 0;
                    player.itemTime = 0;

                    Player ally = Main.player[closestAllyIdx];
                    Vector2 direction = (ally.Center - player.Center).SafeNormalize(Vector2.UnitX);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        player.Center,
                        direction,
                        ModContent.ProjectileType<RoaringSwordChainLunge>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        0f,
                        1000f + closestAllyIdx
                    );
                }
                else
                {
                    chainHitNPCs.Clear();
                }
            }
            else
            {
                chainHitNPCs.Clear();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;
                
            Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<RoaringSwordSwing>()].Value;
            if (texture == null)
                return false;

            // Imbued Willbreaker trait tint
            Color traitTint = Color.White;
            var sp = player.GetModPlayer<RoaringSwordPlayer>();
            if (sp.isHoldingWillbreaker)
                traitTint = ImbuedTraitColor.FromZeroDetermination(sp.imbuedWillbreakerVariant);
                
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height);
            
            float drawRotation = Projectile.rotation + MathHelper.PiOver2;
            SpriteEffects effects = player.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 handOffset = new Vector2(0f, -15f);
            
            for (int i = afterimagePositions.Count - 1; i >= 0; i--)
            {
                float alpha = (1f - (i / (float)MaxAfterimages)) * 0.5f;
                float scale = Projectile.scale * (1f - (i * 0.03f));
                float afterimageRotation = afterimageRotations[i] + MathHelper.PiOver2;
                
                Main.EntitySpriteDraw(
                    texture,
                    afterimagePositions[i] + handOffset - Main.screenPosition,
                    null,
                    traitTint * alpha,
                    afterimageRotation,
                    origin,
                    scale,
                    effects,
                    0
                );
            }
            
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new Vector2(3f, 0f).RotatedBy(i * MathHelper.PiOver2);
                Main.EntitySpriteDraw(
                    texture,
                    player.Center + handOffset + offset - Main.screenPosition,
                    null,
                    traitTint * 0.6f,
                    drawRotation,
                    origin,
                    Projectile.scale * 1.1f,
                    effects,
                    0
                );
            }
            
            Main.EntitySpriteDraw(
                texture,
                player.Center + handOffset - Main.screenPosition,
                null,
                traitTint,
                drawRotation,
                origin,
                Projectile.scale,
                effects,
                0
            );
            
            return false;
        }
    }
}
