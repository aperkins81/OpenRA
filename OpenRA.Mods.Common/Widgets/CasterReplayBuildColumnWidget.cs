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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class CasterReplayBuildColumnWidget : Widget
	{
		public Func<Player> GetPlayer = () => null;

		public int IconWidth = 32;
		public int IconHeight = 24;
		public int RowSpacing = 6;
		public int ProgressBarHeight = 3;

		public string ClockAnimation = "clock";
		public string ClockSequence = "idle";
		public string ClockPalette = "chrome";

		static readonly Color ProgressBackground = Color.FromArgb(160, 20, 20, 20);
		static readonly Color ProgressColor = Color.FromArgb(255, 190, 190, 60);

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly Dictionary<ProductionQueue, Animation> clocks = [];

		[ObjectCreator.UseCtor]
		public CasterReplayBuildColumnWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
		}

		public override void Draw()
		{
			var player = GetPlayer();
			if (player == null)
				return;

			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(a => a.Actor.Owner == player)
				.Select(a => a.Trait)
				.ToList();

			foreach (var queue in queues)
				if (!clocks.ContainsKey(queue))
					clocks.Add(queue, new Animation(world, ClockAnimation));

			// Only show queues that are actively building something.
			var currentItemsByItem = queues
				.Select(q => q.CurrentItem())
				.Where(pi => pi != null)
				.GroupBy(pr => pr.Item)
				.OrderBy(g => g.First().Queue.Info.DisplayOrder)
				.ThenBy(g => g.First().BuildPaletteOrder)
				.ToList();

			Game.Renderer.EnableAntialiasingFilter();

			var iconSize = new float2(IconWidth, IconHeight);
			var row = 0;
			foreach (var currentItems in currentItemsByItem)
			{
				var current = currentItems
					.OrderBy(pi => pi.Done ? 0 : (pi.Paused ? 2 : 1))
					.ThenBy(pi => pi.RemainingTimeActual)
					.First();

				var queue = current.Queue;
				var faction = queue.Actor.Owner.Faction.InternalName;
				var actor = queue.AllItems().FirstOrDefault(a => a.Name == current.Item);
				if (actor == null)
					continue;

				var rsi = actor.TraitInfo<RenderSpritesInfo>();
				var bi = actor.TraitInfo<BuildableInfo>();

				var icon = new Animation(world, rsi.GetImage(actor, faction));
				icon.Play(bi.Icon);

				var topLeft = RenderOrigin + new int2(0, row * (IconHeight + ProgressBarHeight + RowSpacing));
				var centerPosition = topLeft + 0.5f * iconSize;

				var palette = bi.IconPaletteIsPlayerPalette ? bi.IconPalette + player.InternalName : bi.IconPalette;
				WidgetUtils.DrawSpriteCentered(icon.Image, worldRenderer.Palette(palette), centerPosition, 0.5f);

				var clock = clocks[queue];
				clock.PlayFetchIndex(ClockSequence, () => current.TotalTime == 0 ? 0 :
					(current.TotalTime - current.RemainingTime) * (clock.CurrentSequence.Length - 1) / current.TotalTime);
				clock.Tick();
				WidgetUtils.DrawSpriteCentered(clock.Image, worldRenderer.Palette(ClockPalette), centerPosition, 0.5f);

				// Queue count badge for multiple queued items of the same type.
				if (currentItems.Count() > 1)
				{
					var font = Game.Renderer.Fonts["TinyBold"];
					var countText = currentItems.Count().ToStringInvariant();
					font.DrawTextWithContrast(countText, new float2(topLeft.X + IconWidth - font.Measure(countText).X, topLeft.Y),
						Color.White, Color.Black, 1);
				}

				// Progress bar under the icon.
				var cr = Game.Renderer.RgbaColorRenderer;
				var barTop = topLeft.Y + IconHeight + 1;
				cr.FillRect(new float2(topLeft.X, barTop), new float2(topLeft.X + IconWidth, barTop + ProgressBarHeight), ProgressBackground);

				var progress = current.TotalTime == 0 ? 0f : (float)(current.TotalTime - current.RemainingTime) / current.TotalTime;
				var fill = (int)(progress * IconWidth);
				if (fill > 0)
					cr.FillRect(new float2(topLeft.X, barTop), new float2(topLeft.X + fill, barTop + ProgressBarHeight), ProgressColor);

				row++;
			}

			Game.Renderer.DisableAntialiasingFilter();
		}
	}
}
