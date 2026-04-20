using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
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
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Systems.Cards
{
    public static class CardSuitAttackHelper
    {
        public static void SpawnSuitAttack(IEntitySource source, int owner, Vector2 originPos, Vector2 targetPos, CardSuit suit, int baseDamage, float knockback, bool markAsDeckAttack = false, bool spawnAtTarget = true, Vector2? baseDir = null)
        {
            // Use the stable base direction when provided (prevents bad aim when enemy is close to player).
            Vector2 outDir = baseDir ?? (targetPos - originPos).SafeNormalize(Vector2.UnitX);
            Vector2 spawnPos = spawnAtTarget ? targetPos : originPos;

            switch (suit)
            {
                case CardSuit.Hearts:
                    if (spawnAtTarget)
                    {
                        Vector2 orbitCenter = targetPos;
                        for (int i = 0; i < 4; i++)
                        {
                            float angleOffset = MathHelper.PiOver2 * i;
                            Vector2 heartPos = orbitCenter + new Vector2(120f, 0f).RotatedBy(angleOffset);
                            int id = SpawnProjectile(source, heartPos, Vector2.Zero, ModContent.ProjectileType<FriendlyHeartProjectile>(), (int)(baseDamage * 0.15f), knockback, owner, markAsDeckAttack, orbitCenter.X, orbitCenter.Y);
                            if (id >= 0 && id < Main.maxProjectiles)
                                Main.projectile[id].ai[2] = angleOffset;
                        }
                    }
                    else
                    {
                        Vector2 orbitCenter = targetPos;
                        int heartCount = 7;
                        const float ringRadius = 110f;
                        for (int i = 0; i < heartCount; i++)
                        {
                            float angleOffset = MathHelper.TwoPi * i / heartCount;
                            Vector2 heartPos = orbitCenter + new Vector2(ringRadius, 0f).RotatedBy(angleOffset);
                            int heartDamage = (int)(baseDamage * 0.26f);
                            if (markAsDeckAttack)
                                heartDamage = Math.Max(3, heartDamage);

                            int id = SpawnProjectile(source, heartPos, Vector2.Zero, ModContent.ProjectileType<FriendlyHeartProjectile>(), heartDamage, knockback, owner, markAsDeckAttack, orbitCenter.X, orbitCenter.Y);
                            if (id >= 0 && id < Main.maxProjectiles)
                                Main.projectile[id].ai[2] = angleOffset;
                        }
                    }
                    break;

                case CardSuit.Diamonds:
                    if (spawnAtTarget)
                    {
                        float[] diamondSpeeds = { 10f, 7.5f, 5f };
                        for (int i = 0; i < diamondSpeeds.Length; i++)
                        {
                            SpawnProjectile(source, spawnPos, outDir * diamondSpeeds[i], ModContent.ProjectileType<FriendlyDiamondProjectile>(), (int)(baseDamage * 0.5f), knockback, owner, markAsDeckAttack);
                        }
                    }
                    else
                    {
                        Vector2 side = new Vector2(-outDir.Y, outDir.X);
                        int count = 7;
                        for (int i = 0; i < count; i++)
                        {
                            float t = i / (float)(count - 1) - 0.5f;
                            float diamondSpread = MathHelper.ToRadians(30f) * t;
                            Vector2 dir = outDir.RotatedBy(diamondSpread);
                            Vector2 pos = spawnPos + side * (t * 42f);
                            float speed = MathHelper.Lerp(9f, 13f, (float)i / (count - 1));
                            int diamondDamage = (int)(baseDamage * 0.6f);
                            if (markAsDeckAttack)
                                diamondDamage = Math.Max(4, diamondDamage);

                            SpawnProjectile(source, pos, dir * speed, ModContent.ProjectileType<FriendlyDiamondProjectile>(), diamondDamage, knockback, owner, markAsDeckAttack);
                        }
                    }
                    break;

                case CardSuit.Spades:
                    if (spawnAtTarget)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = MathHelper.TwoPi * i / 5f;
                            Vector2 dir = Vector2.UnitX.RotatedBy(angle);
                            SpawnProjectile(source, spawnPos, dir * 7f, ModContent.ProjectileType<FriendlySpadeProjectile>(), (int)(baseDamage * 0.2f), knockback, owner, markAsDeckAttack);
                        }
                    }
                    else
                    {
                        Vector2 side = new Vector2(-outDir.Y, outDir.X);
                        int count = 7;
                        for (int i = 0; i < count; i++)
                        {
                            float t = i / (float)(count - 1) - 0.5f;
                            float spadeSpread = MathHelper.ToRadians(50f) * t;
                            Vector2 dir = outDir.RotatedBy(spadeSpread);
                            Vector2 pos = spawnPos + side * (t * 55f);
                            int spadeDamage = (int)(baseDamage * 0.26f);
                            if (markAsDeckAttack)
                                spadeDamage = Math.Max(3, spadeDamage);

                            SpawnProjectile(source, pos, dir * 8.8f, ModContent.ProjectileType<FriendlySpadeProjectile>(), spadeDamage, knockback, owner, markAsDeckAttack);
                        }
                    }
                    break;

                case CardSuit.Clubs:
                    float clubSpread = MathHelper.ToRadians(15f);
                    int clubDamage = (int)(baseDamage * 0.33f);
                    if (markAsDeckAttack && !spawnAtTarget)
                        clubDamage = Math.Max(4, clubDamage);

                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 dir = outDir.RotatedBy(clubSpread * i);
                        SpawnProjectile(source, spawnPos, dir * 9f, ModContent.ProjectileType<FriendlyClubProjectile>(), clubDamage, knockback, owner, markAsDeckAttack);
                    }
                    break;
            }
        }

        private static int SpawnProjectile(IEntitySource source, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int owner, bool markAsDeckAttack, params float[] ai)
        {
            int id = Projectile.NewProjectile(source, position, velocity, type, Math.Max(1, damage), knockback, owner, ai.Length > 0 ? ai[0] : 0f, ai.Length > 1 ? ai[1] : 0f, ai.Length > 2 ? ai[2] : 0f);
            if (markAsDeckAttack && id >= 0 && id < Main.maxProjectiles)
            {
                Main.projectile[id].GetGlobalProjectile<DeckOfCardsGlobalProjectile>().spawnedByDeckOfCards = true;
            }

            return id;
        }
    }
}