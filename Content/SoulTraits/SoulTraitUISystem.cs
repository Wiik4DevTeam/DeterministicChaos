using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitUISystem : ModSystem
    {
        internal SoulTraitUIState SoulTraitUI;
        private UserInterface soulTraitInterface;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                SoulTraitUI = new SoulTraitUIState();
                SoulTraitUI.Activate();
                soulTraitInterface = new UserInterface();
                soulTraitInterface.SetState(SoulTraitUI);
            }
        }

        public override void Unload()
        {
            SoulTraitUI = null;
            soulTraitInterface = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (ShouldShowUI())
            {
                soulTraitInterface?.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1)
            {
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "DeterministicChaos: Soul Trait Slot",
                    delegate
                    {
                        if (ShouldShowUI())
                        {
                            soulTraitInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }

        private bool ShouldShowUI()
        {
            // Show only when inventory is open and player is not in a special UI
            return Main.playerInventory && 
                   !Main.LocalPlayer.ghost && 
                   !Main.LocalPlayer.dead &&
                   Main.LocalPlayer.active;
        }
    }
}
