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

		public int ColumnCount = 1;
		public bool RightAligned = false;
		public bool CenterAligned = false;
		public bool FillColumnFirst = false;
		public int IconWidth = 144;
		public int IconHeight = 108;
		public int ColumnSpacing = 0;
		public int RowSpacing = 0;
		public int ProgressBarHeight = 12;
		public int IconTopPadding = 4;
		public int TextPadding = 4;
		public float IconScale = 1f;
		public float Opacity = 1f;

		public string ClockAnimation = "clock";
		public string ClockSequence = "idle";
		public string ClockPalette = "chrome";
		public string TextFont = "TinyBold";

		public readonly string TooltipTemplate = "PRODUCTION_TOOLTIP";
		public readonly string TooltipContainer;

		public ProductionIcon TooltipIcon { get; private set; }
		public Func<ProductionIcon> GetTooltipIcon;

		static readonly Color ProgressBackground = Color.FromArgb(160, 20, 20, 20);
		static readonly Color ProgressColor = Color.FromArgb(255, 190, 190, 60);

		sealed class BuildRow
		{
			public ProductionQueue Queue;
			public string QueueType;
			public ActorInfo Actor;
			public ProductionItem Current;
			public int DisplayOrder;
			public int BuildPaletteOrder;
		}

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly Dictionary<ProductionQueue, Animation> clocks = [];
		readonly Dictionary<string, string> lastItemByQueueType = [];
		readonly List<ProductionIcon> productionIcons = [];
		readonly List<Rectangle> productionIconsBounds = [];
		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		int lastIconIdx;
		int currentTooltipToken;

		[ObjectCreator.UseCtor]
		public CasterReplayBuildColumnWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			GetTooltipIcon = () => TooltipIcon;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override void Draw()
		{
			productionIcons.Clear();
			productionIconsBounds.Clear();

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

			var rows = BuildRows(queues);
			if (rows.Count == 0)
				return;

			Game.Renderer.EnableAntialiasingFilter();

			var textFont = Game.Renderer.Fonts[TextFont];
			var iconSize = new float2(IconWidth, IconHeight);
			var columnWidth = IconWidth + ColumnSpacing;
			var rowHeight = IconHeight + ProgressBarHeight + RowSpacing;
			var rowOrigin = RenderOrigin;

			for (var row = 0; row < rows.Count; row++)
			{
				var buildRow = rows[row];
				var queue = buildRow.Queue;
				var actor = buildRow.Actor;
				var current = buildRow.Current;
				var faction = queue.Actor.Owner.Faction.InternalName;
				var bi = actor.TraitInfo<BuildableInfo>();
				var rsi = actor.TraitInfo<RenderSpritesInfo>();

				var icon = new Animation(world, rsi.GetImage(actor, faction));
				icon.Play(bi.Icon);

				int column, line;
				if (FillColumnFirst)
				{
					var rowsPerColumn = Math.Max(1, RenderBounds.Height / rowHeight);
					column = row / rowsPerColumn;
					line = row % rowsPerColumn;
				}
				else
				{
					column = row % ColumnCount;
					line = row / ColumnCount;
				}

				var iconsOnLine = FillColumnFirst ? 1 : Math.Min(ColumnCount, rows.Count - line * ColumnCount);
				var lineWidth = iconsOnLine * IconWidth + Math.Max(0, iconsOnLine - 1) * ColumnSpacing;
				var lineOffset = CenterAligned ? Math.Max(0, (RenderBounds.Width - lineWidth) / 2) : 0;
				var x = RightAligned
					? RenderBounds.Width - lineOffset - IconWidth - column * columnWidth
					: lineOffset + column * columnWidth;
				var topLeft = rowOrigin + new int2(x, line * rowHeight + IconTopPadding);
				var centerPosition = topLeft + 0.5f * iconSize;

				var palette = bi.IconPaletteIsPlayerPalette ? bi.IconPalette + player.InternalName : bi.IconPalette;
				var iconSprite = icon.Image;
				var fillScale = Math.Min(IconWidth / iconSprite.Size.X, IconHeight / iconSprite.Size.Y) * IconScale;
				DrawSpriteCentered(iconSprite, worldRenderer.Palette(palette), centerPosition, fillScale);

				if (current != null)
				{
					var clock = clocks[queue];
					clock.PlayFetchIndex(ClockSequence, () => current.TotalTime == 0 ? 0 :
						(current.TotalTime - current.RemainingTime) * (clock.CurrentSequence.Length - 1) / current.TotalTime);
					clock.Tick();
					DrawSpriteCentered(clock.Image, worldRenderer.Palette(ClockPalette), centerPosition, fillScale);
				}

				var ownedCount = CountOwned(player, actor.Name);
				var ownedText = $"x{ownedCount}";
				var timeText = current != null
					? WidgetUtils.FormatTime(current.RemainingTimeActual, world.Timestep)
					: "--";

				var cr = Game.Renderer.RgbaColorRenderer;
				var barTop = topLeft.Y + IconHeight;
				cr.FillRect(new float2(topLeft.X, barTop), new float2(topLeft.X + IconWidth, barTop + ProgressBarHeight),
					ApplyOpacity(ProgressBackground));

				if (current != null && current.TotalTime > 0)
				{
					var progress = (float)(current.TotalTime - current.RemainingTime) / current.TotalTime;
					var fill = (int)(progress * IconWidth);
					if (fill > 0)
						cr.FillRect(new float2(topLeft.X, barTop), new float2(topLeft.X + fill, barTop + ProgressBarHeight),
							ApplyOpacity(ProgressColor));
				}

				DrawBarText(textFont, topLeft.X, barTop, ownedText, timeText);

				var queued = current != null ? new List<ProductionItem> { current } : [];
				productionIcons.Add(new ProductionIcon
				{
					Actor = actor,
					Pos = new float2(topLeft.X, topLeft.Y),
					Queued = queued,
					ProductionQueue = queue
				});
				productionIconsBounds.Add(new Rectangle(topLeft.X, topLeft.Y, IconWidth, IconHeight + ProgressBarHeight));
			}

			Game.Renderer.DisableAntialiasingFilter();
		}

		public override void Tick()
		{
			if (TooltipContainer == null)
				return;

			if (Ui.MouseOverWidget != this)
			{
				if (TooltipIcon != null)
				{
					tooltipContainer.Value.RemoveTooltip(currentTooltipToken);
					lastIconIdx = 0;
					TooltipIcon = null;
				}

				return;
			}

			if (TooltipIcon != null &&
				productionIconsBounds.Count > lastIconIdx &&
				productionIcons[lastIconIdx].Actor == TooltipIcon.Actor &&
				productionIconsBounds[lastIconIdx].Contains(Viewport.LastMousePos))
				return;

			for (var i = 0; i < productionIconsBounds.Count; i++)
			{
				if (!productionIconsBounds[i].Contains(Viewport.LastMousePos))
					continue;

				lastIconIdx = i;
				TooltipIcon = productionIcons[i];
				currentTooltipToken = tooltipContainer.Value.SetTooltip(
					TooltipTemplate,
					new WidgetArgs { { "player", GetPlayer() }, { "getTooltipIcon", GetTooltipIcon } });
				return;
			}

			TooltipIcon = null;
		}

		List<BuildRow> BuildRows(IList<ProductionQueue> queues)
		{
			var rows = new List<BuildRow>();

			foreach (var typeGroup in queues.GroupBy(q => q.Info.Type))
			{
				var queuesOfType = typeGroup.ToList();
				if (!queuesOfType.Any(q => q.Enabled && q.AnyItemsToBuild()))
					continue;

				var representative = queuesOfType.OrderBy(q => q.Info.DisplayOrder).First();
				var activeItems = queuesOfType
					.Select(q => q.CurrentItem())
					.Where(pi => pi != null)
					.OrderBy(pi => pi.Done ? 0 : (pi.Paused ? 2 : 1))
					.ThenBy(pi => pi.RemainingTimeActual)
					.ToList();

				ActorInfo actor;
				ProductionItem current = null;
				ProductionQueue drawQueue;

				if (activeItems.Count > 0)
				{
					current = activeItems[0];
					drawQueue = current.Queue;
					lastItemByQueueType[typeGroup.Key] = current.Item;
					actor = drawQueue.AllItems().FirstOrDefault(a => a.Name == current.Item);
					if (actor == null)
						continue;
				}
				else
				{
					drawQueue = representative;
					var itemName = lastItemByQueueType.GetValueOrDefault(typeGroup.Key)
						?? representative.BuildableItems().FirstOrDefault()?.Name;
					if (itemName == null)
						continue;

					actor = world.Map.Rules.Actors[itemName];
					if (actor == null)
						continue;
				}

				rows.Add(new BuildRow
				{
					Queue = drawQueue,
					QueueType = typeGroup.Key,
					Actor = actor,
					Current = current,
					DisplayOrder = representative.Info.DisplayOrder,
					BuildPaletteOrder = current?.BuildPaletteOrder
						?? actor.TraitInfo<BuildableInfo>().BuildPaletteOrder
				});
			}

			return rows
				.OrderBy(r => r.DisplayOrder)
				.ThenBy(r => r.BuildPaletteOrder)
				.ToList();
		}

		static int CountOwned(Player player, string actorName)
		{
			return player.World.Actors.Count(a => a.Owner == player && !a.IsDead && a.Info.Name == actorName);
		}

		Color ApplyOpacity(Color color)
		{
			var alpha = (int)(color.A * Math.Clamp(Opacity, 0f, 1f));
			return Color.FromArgb(alpha, color);
		}

		void DrawSpriteCentered(Sprite sprite, PaletteReference palette, float2 centerPosition, float scale)
		{
			var alpha = Math.Clamp(Opacity, 0f, 1f);
			var topLeft = centerPosition - 0.5f * scale * sprite.Size;
			Game.Renderer.SpriteRenderer.DrawSprite(sprite, palette, topLeft, scale, float3.Ones, alpha);
		}

		void DrawBarText(SpriteFont font, int barLeft, int barTop, string left, string center)
		{
			var textY = barTop + (ProgressBarHeight - font.Measure(left).Y) / 2 - 1;
			font.DrawTextWithContrast(left, new float2(barLeft + TextPadding, textY), Color.White, Color.Black, 1);

			var centerSize = font.Measure(center);
			font.DrawTextWithContrast(center, new float2(barLeft + IconWidth - centerSize.X - TextPadding, textY), Color.White, Color.Black, 1);
		}
	}
}
