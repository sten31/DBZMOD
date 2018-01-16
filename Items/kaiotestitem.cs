using Terraria.ModLoader;
using System;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace DBZMOD.Items
{
	public class kaiotestitem : ModItem
	{
		public override void SetDefaults()
		{

			item.width = 24;
			item.height = 30;
			item.useTime = 20;
			item.useAnimation = 20;
			item.useStyle = 1;
			item.value = 300;
			item.rare = 3;
			item.UseSound = SoundID.Item79;
			item.noMelee = true;
		}

    public override void SetStaticDefaults()
    {
      DisplayName.SetDefault("Nimbus Whistle");
      Tooltip.SetDefault("Calls The Legendary Cloud, Nimbus.");
    }
        public override bool UseItem(Player player)
        {
            player.AddBuff(mod.BuffType("KaiokenBuff"), 18000);
            return true;
        }
		public override void AddRecipes()
        {
            ModRecipe recipe = new ModRecipe(mod);
            recipe.AddIngredient(ItemID.Cloud, 20);
            recipe.AddIngredient(null, "HonorKiCrystal");
            recipe.AddTile(null, "KiManipulator");
            recipe.SetResult(this);
            recipe.AddRecipe();
        }		
	}
}