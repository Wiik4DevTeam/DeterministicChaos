using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Sparks;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class TheAbstract : ModItem
    {
        public const int MANA_PER_LETTER = 5;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 32;
            Item.damage = 22;
            Item.knockBack = 2f;
            Item.useTime = 8;
            Item.useAnimation = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = false;
            Item.rare = ModContent.RarityType<PerseveranceRarity>();
            Item.value = Item.buyPrice(gold: 25);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<AbstractLetter>();
            Item.shootSpeed = 7f;
            Item.mana = 0; // Mana handled manually per letter

            SetRogueDamageClass();
        }

        private void SetRogueDamageClass()
        {
            try
            {
                if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                {
                    if (calamity.TryFind<DamageClass>("RogueDamageClass", out var rogueClass))
                    {
                        Item.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
                        return;
                    }
                }
            }
            catch { }

            Item.DamageType = DamageClass.Magic;
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 8, SoulTraitType.Perseverance);
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Perseverance)
                return false;

            if (player.altFunctionUse == 2)
                return true;

            var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();

            if (abstractPlayer.IsAutoFiring || abstractPlayer.IsOnCooldown)
                return false;

            return abstractPlayer.HasLettersToFire() && player.statMana >= MANA_PER_LETTER;
        }

        public override bool? UseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                var uiSystem = ModContent.GetInstance<TheAbstractUISystem>();
                if (uiSystem != null)
                {
                    uiSystem.ToggleUI();
                }
                return true;
            }

            return null;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
                return false;

            if (player.whoAmI != Main.myPlayer)
                return false;

            var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();

            if (abstractPlayer.IsAutoFiring)
                return false;

            abstractPlayer.StartAutoFireSequence(source, velocity, type, damage, knockback);

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            var player = Main.LocalPlayer;
            var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();

            string currentText = string.IsNullOrEmpty(abstractPlayer.StoredText) ? "[Empty]" : abstractPlayer.StoredText;
            var wordLine = new TooltipLine(Mod, "StoredWord", $"Current word: {currentText}");
            wordLine.OverrideColor = new Color(255, 0, 255);
            tooltips.Add(wordLine);
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            var player = Main.LocalPlayer;
            var abstractPlayer = player.GetModPlayer<TheAbstractPlayer>();

            if (!abstractPlayer.IsOnCooldown)
                return;

            float cooldownProgress = abstractPlayer.CooldownProgress;

            Texture2D itemTexture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;

            float overlayAlpha = 0.5f * cooldownProgress;
            Color overlayColor = Color.Black * overlayAlpha;

            spriteBatch.Draw(
                itemTexture,
                position,
                frame,
                overlayColor,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f
            );

            float secondsRemaining = cooldownProgress * 5f;
            string timeText = secondsRemaining.ToString("0.0");
            Vector2 textSize = Terraria.GameContent.FontAssets.ItemStack.Value.MeasureString(timeText);
            Vector2 textPos = position + (frame.Size() * scale / 2f) - (textSize / 2f);

            Terraria.UI.Chat.ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                Terraria.GameContent.FontAssets.ItemStack.Value,
                timeText,
                textPos,
                Color.White,
                0f,
                Vector2.Zero,
                Vector2.One * 0.8f
            );
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SpecialistsNotebook>(), 1)
                .AddIngredient(ModContent.ItemType<SparkOfPerseverance>(), 10)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 10)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
