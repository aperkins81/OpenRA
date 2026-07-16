#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets
{
	public static class ArmyIconsGridRenderer
	{
		public sealed class ArmyIconHitRegion
		{
			public Rectangle Bounds;
			public ArmyUnit Unit;
		}

		public static void Draw(SpriteFont countFont, WorldRenderer worldRenderer, Player player, PlayerStatistics stats,
			int x, int y, int width, int height, int iconDisplayHeight, int iconSpacing = 1, bool rightAligned = false,
			List<ArmyIconHitRegion> hitRegions = null)
		{
			if (player == null || stats == null || width <= 0 || height <= 0)
				return;

			var items = stats.Units.Values
				.Where(u => u.Count > 0 && u.Icon != null)
				.OrderBy(u => u.ProductionQueueOrder)
				.ThenBy(u => u.BuildPaletteOrder)
				.ToList();

			if (items.Count == 0)
				return;

			Game.Renderer.EnableAntialiasingFilter();

			var iconHeight = iconDisplayHeight > 0 ? iconDisplayHeight : height - 4;
			var iconWidth = iconHeight * 4 / 3;
			var totalWidth = items.Count * iconWidth + (items.Count - 1) * iconSpacing;
			if (totalWidth > width)
			{
				iconWidth = Math.Max(1, (width - (items.Count - 1) * iconSpacing) / items.Count);
			}

			totalWidth = items.Count * iconWidth + (items.Count - 1) * iconSpacing;
			var iconSize = new float2(iconWidth, iconHeight);
			var verticalOffset = y + height / 2 - iconHeight / 2;
			var startX = rightAligned ? x + width - totalWidth : x;

			for (var i = 0; i < items.Count; i++)
			{
				var unit = items[i];
				var iconTopLeft = new int2(startX + i * (iconWidth + iconSpacing), verticalOffset);
				var centerPosition = iconTopLeft + 0.5f * iconSize;

				var palette = unit.IconPaletteIsPlayerPalette ? unit.IconPalette + player.InternalName : unit.IconPalette;
				var iconSprite = unit.Icon.Image;
				var fillScale = Math.Min(iconWidth / iconSprite.Size.X, iconHeight / iconSprite.Size.Y);
				WidgetUtils.DrawSpriteCentered(iconSprite, worldRenderer.Palette(palette), centerPosition, fillScale);

				var text = unit.Count.ToString(NumberFormatInfo.CurrentInfo);
				countFont.DrawTextWithContrast(text, iconTopLeft + new float2(iconWidth, 0) - new float2(countFont.Measure(text).X, countFont.TopOffset),
					Color.White, Color.Black, 1);

				hitRegions?.Add(new ArmyIconHitRegion
				{
					Bounds = new Rectangle(iconTopLeft.X, iconTopLeft.Y, iconWidth, iconHeight),
					Unit = unit
				});
			}

			Game.Renderer.DisableAntialiasingFilter();
		}
	}
}
