using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using ReLogic.Content;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.Items
{
    [AutoloadEquip(EquipType.Back)]
    public class ShadowMantle : ModItem
    {
        public override void Load()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                EquipLoader.AddEquipTexture(Mod, "DeterministicChaos/Content/Items/ShadowMantle_Neck", EquipType.Neck, this);
            }
        }

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Blue;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<ShadowMantlePlayer>().hasShadowMantle = true;
            player.GetDamage(DamageClass.Generic) += 0.05f;
            player.wingTimeMax = (int)(player.wingTimeMax * 1.5f);

            if (!hideVisual)
            {
                int backSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Back);
                int neckSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Neck);
                player.back = backSlot;
                player.neck = (sbyte)neckSlot;
                player.GetModPlayer<ShadowMantlePlayer>().showArms = true;
            }
        }
    }

    // Custom draw layer for the Shadow Mantle arms
    public class ShadowMantleArmsLayer : PlayerDrawLayer
    {
        private Asset<Texture2D> armsTexture;

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            return drawInfo.drawPlayer.GetModPlayer<ShadowMantlePlayer>().showArms;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (armsTexture == null)
            {
                armsTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Items/ShadowMantle_Arms");
            }

            Player player = drawInfo.drawPlayer;
            Texture2D texture = armsTexture.Value;

            // Standard 20-frame format but only first 4 have content
            int frameHeight = 56;
            int playerFrame = player.bodyFrame.Y / player.bodyFrame.Height;
            
            // Clamp to available frames (0-3)
            int frame = playerFrame;
            if (frame > 3)
                frame = 0;

            Vector2 position = drawInfo.Position - Main.screenPosition;
            position += new Vector2(player.width / 2f - player.bodyFrame.Width / 2f, player.height - player.bodyFrame.Height + 4f);
            position += player.bodyPosition;
            position = position.Floor();

            Rectangle sourceRect = new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);

            SpriteEffects effects = drawInfo.playerEffect;

            DrawData data = new DrawData(
                texture,
                position,
                sourceRect,
                Lighting.GetColor((int)(player.position.X + player.width / 2) / 16, (int)(player.position.Y + player.height / 2) / 16),
                player.bodyRotation,
                drawInfo.bodyVect,
                1f,
                effects,
                0
            );

            drawInfo.DrawDataCache.Add(data);
        }
    }

    // Provides damage reduction from Roaring Knight boss
    public class ShadowMantlePlayer : ModPlayer
    {
        public bool hasShadowMantle;
        public bool showArms;

        public override void ResetEffects()
        {
            hasShadowMantle = false;
            showArms = false;
        }

        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            if (showArms)
            {
                PlayerDrawLayers.ArmOverItem.Hide();
                PlayerDrawLayers.HandOnAcc.Hide();
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (hasShadowMantle)
            {
                // Reduce damage from Roaring Knight and its sphere
                if (npc.type == ModContent.NPCType<RoaringKnight>() || 
                    npc.type == ModContent.NPCType<RoaringKnightSphere>())
                {
                    modifiers.FinalDamage *= 0.33f;
                }
            }
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            if (!hasShadowMantle)
                return;

            // All Roaring Knight related projectiles
            bool isRoaringKnightProjectile = 
                proj.type == ModContent.ProjectileType<ToothProjectile>() ||
                proj.type == ModContent.ProjectileType<SlashAttack>() ||
                proj.type == ModContent.ProjectileType<SeekingKnife>() ||
                proj.type == ModContent.ProjectileType<Projectile_Star>() ||
                proj.type == ModContent.ProjectileType<Projectile_Seeking>() ||
                proj.type == ModContent.ProjectileType<LatticeKnife>() ||
                proj.type == ModContent.ProjectileType<ShockwaveLine>() ||
                proj.type == ModContent.ProjectileType<FireSphere>() ||
                proj.type == ModContent.ProjectileType<FireTrail>() ||
                proj.type == ModContent.ProjectileType<Shockwave>();

            if (isRoaringKnightProjectile)
            {
                modifiers.FinalDamage *= 0.33f;
            }
        }
    }
}
