#region Copyright & License Information
/*
 * Copyright 2007-2012 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.FileFormats;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class PlayerPaletteFromCurrentTilesetInfo : ITraitInfo
	{
		[Desc("internal palette name")]
		public readonly string Name = null;
		[Desc("Map listed indices to shadow.")]
		public readonly int[] ShadowIndex = { };
		[Desc("Apply palette rotatotors or not.")]
		public readonly bool AllowModifiers = true;

		public object Create(ActorInitializer init) { return new PlayerPaletteFromCurrentTileset(init.world, this); }
	}

	class PlayerPaletteFromCurrentTileset : IPalette
	{
		readonly World world;
		readonly PlayerPaletteFromCurrentTilesetInfo info;

		public PlayerPaletteFromCurrentTileset(World world, PlayerPaletteFromCurrentTilesetInfo info)
		{
			this.world = world;
			this.info = info;
		}

		public void InitPalette (OpenRA.Graphics.WorldRenderer wr)
		{
			string Filename = world.TileSet.PlayerPalette == null ? world.TileSet.Palette : world.TileSet.PlayerPalette;
			wr.AddPalette(info.Name, new Palette(FileSystem.Open(Filename), info.ShadowIndex), info.AllowModifiers);
		}
	}
}
