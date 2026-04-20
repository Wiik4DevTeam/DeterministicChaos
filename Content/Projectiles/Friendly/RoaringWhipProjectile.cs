using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
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
using DeterministicChaos.Content.Items.Imbued;
using DeterministicChaos.Content.Items.Prefixes;
using DeterministicChaos.Content.SoulTraits.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringWhipProjectile : ModProjectile
    {
        private const int TrailLength = 6;
        private List<List<Vector2>> trailHistory = new List<List<Vector2>>();
        private bool slashSpawned = false; // Only spawn one slash per whip swing

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 1.8f; // Extended range
        }

        private float Timer
        {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override bool PreAI()
        {
            Player owner = Main.player[Projectile.owner];
            var whipPlayer = owner.GetModPlayer<RoaringWhipPlayer>();

            // Determination: +10% whip range
            if (whipPlayer.imbuedGnomonVariant == ImbuedGnomonVariant.Determination)
                Projectile.WhipSettings.RangeMultiplier = 1.8f * 1.1f;
            // Bravery: -30% whip range
            else if (whipPlayer.imbuedGnomonVariant == ImbuedGnomonVariant.Bravery)
                Projectile.WhipSettings.RangeMultiplier = 1.8f * 0.7f;
            else
                Projectile.WhipSettings.RangeMultiplier = 1.8f;

            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
            Projectile.damage = (int)(Projectile.damage * 0.7f);
            
            // Spawn slash attack on first hit only
            // In multiplayer, whip OnHitNPC runs on the owner client
            if (!slashSpawned && Main.myPlayer == Projectile.owner)
            {
                SpawnSlashAttack(target);
                slashSpawned = true;
            }

            Player owner = Main.player[Projectile.owner];
            var whipPlayer = owner.GetModPlayer<RoaringWhipPlayer>();

            // Integrity: tag this NPC for DR
            if (whipPlayer.imbuedGnomonVariant == ImbuedGnomonVariant.Integrity)
            {
                whipPlayer.integrityTargetNPC = target.whoAmI;
            }

            // Patience: apply tag debuff for minion bonus damage
            if (whipPlayer.imbuedGnomonVariant == ImbuedGnomonVariant.Patience)
            {
                target.AddBuff(ModContent.BuffType<GnomonTagDebuff>(), 240);
            }

            // Justice: hypercrit VFX (flag was checked in ModifyHitNPC)
            if (whipPlayer.justiceHypercritPending)
            {
                whipPlayer.justiceHypercritPending = false;

                for (int i = 0; i < 25; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.YellowTorch, vel, 0, default, 2f);
                    dust.noGravity = true;
                }
                CombatText.NewText(target.Hitbox, new Color(255, 255, 50), damageDone, dramatic: true);
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Hypercrit") { Volume = 0.6f }, target.Center);

                var hatPlayer = owner.GetModPlayer<CowboyHatPlayer>();
                if (hatPlayer.hasSheriffHat)
                    hatPlayer.hypercritAttackSpeedTimer = 36;
            }
        }
        
        private void SpawnSlashAttack(NPC target)
        {
            // Owner client handles spawning, projectile will be synced to other clients
            Player owner = Main.player[Projectile.owner];
            
            // Calculate angle from target to player (line starts facing the player)
            float angleToPlayer = (owner.Center - target.Center).ToRotation();
            
            // Slash damage is 1.2x the whip's current damage
            int slashDamage = (int)(Projectile.damage * 1.2f);
            
            // Random rotation direction
            float rotationDirection = Main.rand.NextBool() ? 1f : -1f;
            
            // Spawn the slash indicator at the target
            int id = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<RoaringWhipSlash>(),
                0,
                0f,
                Projectile.owner,
                slashDamage,
                angleToPlayer,
                rotationDirection
            );
            
            if (id >= 0)
                Main.projectile[id].netUpdate = true;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            Player owner = Main.player[Projectile.owner];
            var whipPlayer = owner.GetModPlayer<RoaringWhipPlayer>();

            // Justice: consume summon-crit flag for hypercrit (1.5x damage on top of crit = 3x total)
            if (whipPlayer.imbuedGnomonVariant == ImbuedGnomonVariant.Justice && whipPlayer.justiceHypercritPending)
            {
                modifiers.SetCrit();
                modifiers.FinalDamage *= 1.5f;
            }
        }

        public override void PostAI()
        {
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            // Variant-aware lighting
            Player owner = Main.player[Projectile.owner];
            var whipPlayer = owner.GetModPlayer<RoaringWhipPlayer>();
            Vector3 lightVec = new Vector3(0.9f, 0.9f, 0.9f);
            if (whipPlayer.imbuedGnomonVariant != ImbuedGnomonVariant.None)
                lightVec = GetVariantColor(whipPlayer.imbuedGnomonVariant).ToVector3() * 0.9f;

            foreach (Vector2 point in points)
            {
                Lighting.AddLight(point, lightVec);
            }

            // Store trail history for afterimages
            List<Vector2> currentPoints = new List<Vector2>(points);
            trailHistory.Insert(0, currentPoints);
            if (trailHistory.Count > TrailLength)
            {
                trailHistory.RemoveAt(trailHistory.Count - 1);
            }

            // Kindness: buff allies along whip path
            if (Main.myPlayer == Projectile.owner && whipPlayer.imbuedGnomonVariant == ImbuedGnomonVariant.Kindness)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (i == Projectile.owner) continue;
                    Player ally = Main.player[i];
                    if (!ally.active || ally.dead)
                        continue;
                    if (ally.team != owner.team || owner.team == 0)
                        continue;

                    bool hit = false;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (Vector2.Distance(points[j], ally.Center) < 40f)
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                    {
                        int duration = 300; // 5 seconds
                        var prefixPlayer = ally.GetModPlayer<PrefixEffectPlayer>();
                        duration = prefixPlayer.ScaleBuffDuration(duration, ModContent.BuffType<GnomonAllyBuff>());
                        ally.AddBuff(ModContent.BuffType<GnomonAllyBuff>(), duration);
                    }
                }
            }
        }

        private static Color GetVariantColor(ImbuedGnomonVariant variant)
        {
            return variant switch
            {
                ImbuedGnomonVariant.Determination => new Color(255, 60, 60),
                ImbuedGnomonVariant.Integrity => new Color(0, 0, 255),
                ImbuedGnomonVariant.Patience => new Color(80, 255, 255),
                ImbuedGnomonVariant.Perseverance => new Color(255, 80, 255),
                ImbuedGnomonVariant.Kindness => new Color(80, 230, 80),
                ImbuedGnomonVariant.Justice => new Color(255, 255, 80),
                ImbuedGnomonVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        private void DrawLine(List<Vector2> list)
        {
            Texture2D texture = TextureAssets.FishingLine.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = new Vector2(frame.Width / 2, 2);

            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 1; i++)
            {
                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor(element.ToTileCoordinates(), Color.White);
                Vector2 scale = new Vector2(1, (diff.Length() + 2) / frame.Height);

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, SpriteEffects.None, 0);

                pos += diff;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            List<Vector2> list = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, list);

            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            // Imbued Gnomon trait tint
            Color traitTint = Color.White;
            Player owner = Main.player[Projectile.owner];
            if (owner != null && owner.active)
            {
                var wp = owner.GetModPlayer<RoaringWhipPlayer>();
                if (wp.isHoldingGnomon && wp.imbuedGnomonVariant != ImbuedGnomonVariant.None)
                    traitTint = GetVariantColor(wp.imbuedGnomonVariant);
            }

            // Draw afterimages from trail history
            for (int trail = trailHistory.Count - 1; trail >= 0; trail--)
            {
                List<Vector2> trailPoints = trailHistory[trail];
                float trailAlpha = (1f - (trail / (float)TrailLength)) * 0.4f;

                Vector2 trailPos = trailPoints[0];
                for (int i = 0; i < trailPoints.Count - 1; i++)
                {
                    Rectangle frame = GetFrameForSegment(i, trailPoints.Count);
                    Vector2 origin = new Vector2(11, 8);

                    Vector2 element = trailPoints[i];
                    Vector2 diff = trailPoints[i + 1] - element;
                    float rotation = diff.ToRotation() - MathHelper.PiOver2;

                    Main.EntitySpriteDraw(texture, trailPos - Main.screenPosition, frame, traitTint * trailAlpha, rotation, origin, 1f, flip, 0);
                    trailPos += diff;
                }
            }

            DrawLine(list);

            // Draw main whip
            Vector2 pos = list[0];

            for (int i = 0; i < list.Count - 1; i++)
            {
                Rectangle frame = GetFrameForSegment(i, list.Count);
                Vector2 origin = new Vector2(11, 8);
                float scale = 1;

                if (i == list.Count - 2)
                {
                    Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out int _, out float _);
                    float t = Timer / timeToFlyOut;
                    scale = MathHelper.Lerp(0.5f, 1.5f, Utils.GetLerpValue(0.1f, 0.7f, t, true) * Utils.GetLerpValue(0.9f, 0.7f, t, true));
                }

                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = ImbuedTraitColor.Multiply(Lighting.GetColor(element.ToTileCoordinates()), traitTint);

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, flip, 0);

                pos += diff;
            }
            return false;
        }

        private Rectangle GetFrameForSegment(int segmentIndex, int totalSegments)
        {
            if (segmentIndex == totalSegments - 2)
            {
                return new Rectangle(0, 74, 22, 18);
            }
            else if (segmentIndex > 10)
            {
                return new Rectangle(0, 58, 22, 16);
            }
            else if (segmentIndex > 5)
            {
                return new Rectangle(0, 42, 22, 16);
            }
            else if (segmentIndex > 0)
            {
                return new Rectangle(0, 26, 22, 16);
            }
            return new Rectangle(0, 0, 22, 26);
        }
    }
}
