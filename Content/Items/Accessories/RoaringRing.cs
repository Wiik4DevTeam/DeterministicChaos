using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria;
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
    public class RoaringRing : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 22;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.accessory = true;
        }

        // Called from the normal (non-social) accessory slot.
        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<RoaringRingPlayer>().HasRing = true;
        }

        // Called from the social/vanity accessory slot.
        public override void UpdateVanity(Player player)
        {
            player.GetModPlayer<RoaringRingPlayer>().HasRing = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // ModPlayer, records past player positions for draw-layer afterimages
    // ─────────────────────────────────────────────────────────────────────
    public class RoaringRingPlayer : ModPlayer
    {
        public bool HasRing = false;

        private const int MaxGhosts = 8;
        private const int SpawnInterval = 3;

        private int spawnTimer = 0;

        public struct GhostNode
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Opacity;
            public bool Active;
        }

        public readonly GhostNode[] Ghosts = new GhostNode[MaxGhosts];

        public override void ResetEffects()
        {
            HasRing = false;
        }

        public override void PostUpdateEquips()
        {
            // Fallback detection: if slot hooks fail for any reason,
            // detect the ring directly from equipped/vanity armor slots.
            if (!HasRing)
            {
                int ringType = ModContent.ItemType<RoaringRing>();
                for (int i = 0; i < Player.armor.Length; i++)
                {
                    if (Player.armor[i].type == ringType)
                    {
                        HasRing = true;
                        break;
                    }
                }
            }

            if (!HasRing)
                return;

            if (Main.dedServ)
                return;

            UpdateGhosts();

            spawnTimer++;
            if (spawnTimer >= SpawnInterval)
            {
                spawnTimer = 0;
                SpawnGhost();
            }
        }

        private void UpdateGhosts()
        {
            for (int i = 0; i < Ghosts.Length; i++)
            {
                if (!Ghosts[i].Active)
                    continue;

                GhostNode g = Ghosts[i];
                g.Position += g.Velocity;
                g.Velocity *= 0.93f;
                g.Opacity -= 0.035f;
                if (g.Opacity <= 0.01f)
                    g.Active = false;

                Ghosts[i] = g;
            }
        }

        private void SpawnGhost()
        {
            int index = -1;
            for (int i = 0; i < Ghosts.Length; i++)
            {
                if (!Ghosts[i].Active)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                // Reuse the oldest remaining ghost if all slots are busy.
                int oldest = 0;
                for (int i = 1; i < Ghosts.Length; i++)
                {
                    if (Ghosts[i].Opacity < Ghosts[oldest].Opacity)
                        oldest = i;
                }
                index = oldest;
            }

            Vector2 backward = new Vector2(-Player.direction, 0f);
            float speed = Main.rand.NextFloat(1.3f, 2.2f);

            GhostNode node = new GhostNode
            {
                Position = Player.Center,
                Velocity = backward * speed + new Vector2(0f, Main.rand.NextFloat(-0.15f, 0.15f)),
                Opacity = 0.55f,
                Active = true
            };

            Ghosts[index] = node;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Draw layer, duplicates the player's own draw data at older positions
    // ─────────────────────────────────────────────────────────────────────
    public class RoaringRingAfterimageLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.LastVanillaLayer);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
            => drawInfo.drawPlayer.GetModPlayer<RoaringRingPlayer>().HasRing;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            RoaringRingPlayer rp = drawInfo.drawPlayer.GetModPlayer<RoaringRingPlayer>();
            if (!rp.HasRing)
                return;

            if (drawInfo.DrawDataCache.Count == 0)
                return;

            int originalCount = drawInfo.DrawDataCache.Count;
            Vector2 currentCenter = drawInfo.drawPlayer.Center;

            List<DrawData> ghosts = new List<DrawData>(originalCount * rp.Ghosts.Length);

            for (int t = rp.Ghosts.Length - 1; t >= 0; t--)
            {
                RoaringRingPlayer.GhostNode node = rp.Ghosts[t];
                if (!node.Active)
                    continue;

                Vector2 offset = node.Position - currentCenter;
                float alpha = node.Opacity;

                for (int i = 0; i < originalCount; i++)
                {
                    DrawData src = drawInfo.DrawDataCache[i];
                    Color c = src.color * alpha;

                    DrawData ghost = new DrawData(
                        src.texture,
                        src.position + offset,
                        src.sourceRect,
                        c,
                        src.rotation,
                        src.origin,
                        src.scale,
                        src.effect,
                        src.shader
                    );

                    ghosts.Add(ghost);
                }
            }

            if (ghosts.Count > 0)
                drawInfo.DrawDataCache.InsertRange(0, ghosts);
        }
    }
}
