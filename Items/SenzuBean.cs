using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DBZMOD.Items
{
	public class SenzuBean : ModItem
	{
		public override void SetDefaults()
		{


			item.width = 24;
			item.height = 24;
			item.healLife = 9001;
			item.consumable = true;
			item.maxStack = 3;
			item.UseSound = SoundID.Item3;
			item.useStyle = 2;
			item.useTurn = true;
			item.useAnimation = 17;
			item.useTime = 17;
			item.value = 10000;
			item.rare = 5;
			item.potion = true;
		}

    public override void SetStaticDefaults()
    {
      DisplayName.SetDefault("Senzu Bean");
      Tooltip.SetDefault("Restores your body!");
    }

	}
}
