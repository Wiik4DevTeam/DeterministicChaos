using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    public class RoaringGun : ModItem
    {
        public const int MaxStacks = 20;
        public const int BaseUseTime = 40;

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 20;
            Item.damage = 34;
            Item.knockBack = 3f;
            Item.useTime = BaseUseTime;
            Item.useAnimation = BaseUseTime;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 5);
            Item.UseSound = SoundID.Item11;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Bullet;
            Item.DamageType = DamageClass.Ranged;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // Draw item at full brightness, unaffected by lighting
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.Draw(texture, position, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            // Slight damage bonus based on stacks
            int stacks = player.GetModPlayer<RoaringGunPlayer>().gunStacks;
            damage += stacks * 0.02f;
        }

        public override float UseSpeedMultiplier(Player player)
        {
            // Increase fire rate based on stacks with heavy ramp at 19-20
            int stacks = player.GetModPlayer<RoaringGunPlayer>().gunStacks;
            // 0 stacks = 1x, 1-18 stacks = gradual increase to 2x, 19 = 3.5x, 20 = 5x
            if (stacks == 0)
                return 1f;
            else if (stacks <= 18)
                return 1f + (stacks / 18f) * 1f; // 1x to 2x over 18 stacks
            else if (stacks == 19)
                return 2.5f;
            else
                return 5f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Start decay timer if not already running (only reset on hit, not on shot)
            var gunPlayer = player.GetModPlayer<RoaringGunPlayer>();
            
            if (gunPlayer.decayTimer <= 0 && gunPlayer.gunStacks > 0)
            {
                // Calculate current effective use time
                float speedMult = UseSpeedMultiplier(player);
                int effectiveUseTime = (int)(BaseUseTime / speedMult);
                gunPlayer.decayTimer = effectiveUseTime * 2;
            }

            // Let the game shoot the normal bullet from ammo
            return true;
        }

        public override void HoldItem(Player player)
        {
            // Update decay timer while holding
            var gunPlayer = player.GetModPlayer<RoaringGunPlayer>();
            gunPlayer.isHoldingRoaringGun = true;
            // Emit bright light when held
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color statGray = new Color(60, 60, 60);
            foreach (TooltipLine line in tooltips)
            {
                // Stats get a grayer color to differentiate from descriptions
                if (line.Name == "Damage" || line.Name == "Speed" || line.Name == "Knockback" || 
                    line.Name == "CritChance" || line.Name == "Defense" || line.Name == "UseMana" ||
                    line.Name == "Consumable" || line.Name == "Material")
                {
                    line.OverrideColor = statGray;
                }
                else
                {
                    line.OverrideColor = Color.Black;
                }
            }

            // Add stack info
            Player player = Main.LocalPlayer;
            int stacks = player.GetModPlayer<RoaringGunPlayer>().gunStacks;
            tooltips.Add(new TooltipLine(Mod, "StackInfo", $"Current stacks: {stacks}/{MaxStacks}")
            {
                OverrideColor = statGray
            });
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            Vector2 position = new Vector2(line.X, line.Y);

            // Draw white shadow outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;

                    Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                        Main.spriteBatch,
                        line.Font,
                        line.Text,
                        position + new Vector2(x, y),
                        Color.White,
                        line.Rotation,
                        line.Origin,
                        line.BaseScale
                    );
                }
            }

            // Draw black text on top
            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch,
                line.Font,
                line.Text,
                position,
                Color.Black,
                line.Rotation,
                line.Origin,
                line.BaseScale
            );

            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 15)
                .AddIngredient(ItemID.FlintlockPistol)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // ModPlayer to track gun stacks
    public class RoaringGunPlayer : ModPlayer
    {
        public int gunStacks = 0;
        public int decayTimer = 0;
        public bool isHoldingRoaringGun = false;

        public void OnHit()
        {
            // Only the local player should increment stacks
            if (Player.whoAmI != Main.myPlayer)
                return;
            
            gunStacks = System.Math.Min(gunStacks + 1, RoaringGun.MaxStacks);
            // Reset decay timer on hit
            float speedMult = 0.4f + (gunStacks * 0.1f);
            int effectiveUseTime = (int)(RoaringGun.BaseUseTime / speedMult);
            decayTimer = effectiveUseTime * 2;
        }

        public override void PostUpdate()
        {
            // Apply buff to show stacks
            if (isHoldingRoaringGun && gunStacks > 0)
            {
                Player.AddBuff(ModContent.BuffType<RoaringGunBuff>(), 2);
            }

            if (isHoldingRoaringGun && gunStacks > 0)
            {
                decayTimer--;
                if (decayTimer <= 0)
                {
                    gunStacks = System.Math.Max(gunStacks - 1, 0);
                    // Reset timer for next decay
                    float speedMult = 0.4f + (gunStacks * 0.1f);
                    int effectiveUseTime = (int)(RoaringGun.BaseUseTime / speedMult);
                    decayTimer = effectiveUseTime * 2;
                }
            }
        }

        public override void ResetEffects()
        {
            isHoldingRoaringGun = false;
        }
    }

    // GlobalProjectile to track hits from RoaringGun bullets
    public class RoaringGunGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        
        public bool firedFromRoaringGun = false;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse_WithAmmo itemSource)
            {
                if (itemSource.Item.type == ModContent.ItemType<RoaringGun>())
                {
                    firedFromRoaringGun = true;
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Only process hit for the projectile owner
            if (firedFromRoaringGun && projectile.owner == Main.myPlayer)
            {
                Player owner = Main.player[projectile.owner];
                if (owner != null && owner.active)
                {
                    owner.GetModPlayer<RoaringGunPlayer>().OnHit();
                }
            }
        }
    }
}
