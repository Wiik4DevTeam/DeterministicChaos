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

namespace DeterministicChaos.Content.Items.Materials
{
    // Horizontal-frame draw animation for items with side-by-side frames.
    public class DrawAnimationHorizontal : DrawAnimation
    {
        public DrawAnimationHorizontal(int ticksPerFrame, int frameCount)
        {
            Frame = 0;
            FrameCounter = 0;
            FrameCount = frameCount;
            TicksPerFrame = ticksPerFrame;
        }

        public override void Update()
        {
            if (++FrameCounter >= TicksPerFrame)
            {
                FrameCounter = 0;
                Frame = (Frame + 1) % FrameCount;
            }
        }

        public override Rectangle GetFrame(Texture2D texture, int frameCounterOverride = -1)
        {
            int frameWidth = texture.Width / FrameCount;
            int currentFrame = frameCounterOverride >= 0 ? frameCounterOverride : Frame;
            return new Rectangle(currentFrame * frameWidth, 0, frameWidth, texture.Height);
        }
    }

    public class SoulCatalyst : ModItem
    {
        public override void SetStaticDefaults()
        {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationHorizontal(8, 2));
            ItemID.Sets.AnimatesAsSoul[Item.type] = true;
            ItemID.Sets.ItemNoGravity[Item.type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 99;
            Item.value = Item.buyPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.Red;
        }

        public override void PostUpdate()
        {
            Lighting.AddLight(Item.Center, 0.4f, 0.2f, 0.5f);
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Color.White;
        }
    }
}
