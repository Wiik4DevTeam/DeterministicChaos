using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
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

namespace DeterministicChaos.Content.Items.Accessories
{
    // ─────────────────────────────────────────────────────────────────────
    // Item
    // ─────────────────────────────────────────────────────────────────────
    public class RoaringShield : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.accessory = true;
            Item.defense = 5;
        }

        // Only the equipped (non-vanity) slot activates the dash.
        public override void UpdateEquip(Player player)
        {
            player.GetModPlayer<RoaringShieldPlayer>().HasShield = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Drawing, replaces the player with the sphere sprite during the dash
    // ─────────────────────────────────────────────────────────────────────
    public class RoaringShieldSphereDrawLayer : PlayerDrawLayer
    {
        private const int FrameW = 20;
        private const int FrameH = 20;
        private const float SphereScale = 4f;   // 80×80 rendered, covers the player body

        // Position after every vanilla layer so we can clear the whole cache
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.LastVanillaLayer);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
            => drawInfo.drawPlayer.GetModPlayer<RoaringShieldPlayer>().IsInSphereForm;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            RoaringShieldPlayer sp = drawInfo.drawPlayer.GetModPlayer<RoaringShieldPlayer>();
            if (!sp.IsInSphereForm)
                return;

            Texture2D tex = ModContent.Request<Texture2D>(
                "DeterministicChaos/Content/NPCs/Bosses/RoaringKnightSphere",
                ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;

            Vector2 center = drawInfo.drawPlayer.Center - Main.screenPosition;
            Vector2 origin = new Vector2(FrameW * 0.5f, FrameH * 0.5f);
            Rectangle frame = new Rectangle(0, sp.SphereFrame * FrameH, FrameW, FrameH);

            // Remove all normal player draw calls, sphere replaces the character
            drawInfo.DrawDataCache.Clear();

            // Afterimage trail during the active dash phase
            if (sp.IsDashing)
            {
                for (int i = sp.TrailPositions.Length - 1; i >= 1; i--)
                {
                    Vector2 trailPos = sp.TrailPositions[i];
                    if (trailPos == Vector2.Zero)
                        continue;

                    float t = i / (float)sp.TrailPositions.Length;
                    float alpha = (1f - t) * 0.45f;

                    drawInfo.DrawDataCache.Add(new DrawData(
                        tex,
                        trailPos - Main.screenPosition,
                        frame,
                        Color.White * alpha,
                        0f,
                        origin,
                        SphereScale,
                        SpriteEffects.None,
                        0));
                }
            }

            // Main sphere, fade in during charge, fade out during recovery
            drawInfo.DrawDataCache.Add(new DrawData(
                tex,
                center,
                frame,
                Color.White * sp.SphereAlpha,
                0f,
                origin,
                SphereScale,
                SpriteEffects.None,
                0));
        }
    }
}
