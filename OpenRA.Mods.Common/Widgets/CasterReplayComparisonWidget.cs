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
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class CasterReplayComparisonWidget : Widget
	{
		public Func<Player> GetPlayer1 = () => null;
		public Func<Player> GetPlayer2 = () => null;
		public Func<int> GetPlayer1Score = () => 0;
		public Func<int> GetPlayer2Score = () => 0;
		public Func<bool> ShowStats = () => false;

		public int BarMaxWidth = 260;
		public int BarHeight = 12;
		public int BarSpacing = 6;
		public int HeaderHeight = 22;
		public int StatColumnWidth = 150;
		public int StatLineHeight = 13;
		public string NameFont = "Bold";
		public string LabelFont = "TinyBold";

		static readonly string[] StatLabels =
		[
			"Cash", "Income", "Earned", "Spent", "Harvesters", "Derricks", "Power", "Army", "Assets",
			"Kills", "Deaths", "Units K/L", "Bldgs K/L", "Destroyed", "Lost", "Vision", "Exp", "APM"
		];

		static readonly Color BackgroundColor = Color.FromArgb(160, 20, 20, 20);
		static readonly Color BorderColor = Color.FromArgb(200, 0, 0, 0);
		static readonly Color PanelColor = Color.FromArgb(180, 10, 10, 10);

		readonly World world;

		[ObjectCreator.UseCtor]
		public CasterReplayComparisonWidget(World world)
		{
			this.world = world;
		}

		static long UnitCount(PlayerStatistics stats)
		{
			return stats == null ? 0 : stats.Units.Values.Sum(u => (long)u.Count);
		}

		static long Money(Player player)
		{
			var resources = player.PlayerActor.TraitOrDefault<PlayerResources>();
			return resources?.GetCashAndResources() ?? 0;
		}

		static long Assets(PlayerStatistics stats)
		{
			return stats?.AssetsValue ?? 0;
		}

		static long Power(Player player)
		{
			var pm = player.PlayerActor.TraitOrDefault<PowerManager>();
			return pm?.PowerProvided ?? 0;
		}

		static string Currency(long value)
		{
			return "$" + value.ToString(NumberFormatInfo.CurrentInfo);
		}

		static Color GetPowerColor(PowerState state)
		{
			if (state == PowerState.Critical)
				return Color.Red;

			if (state == PowerState.Low)
				return Color.Orange;

			return Color.LimeGreen;
		}

		int HarvesterCount(Player player)
		{
			return world.ActorsWithTrait<Harvester>()
				.Count(a => a.Actor.Owner == player && !a.Actor.IsDead && !a.Trait.IsTraitDisabled);
		}

		int DerrickCount(Player player)
		{
			return world.ActorsHavingTrait<UpdatesDerrickCount>()
				.Count(a => a.Owner == player && !a.IsDead);
		}

		string Vision(Player player)
		{
			if (player.Shroud.Disabled)
				return "100%";

			return (Math.Ceiling(player.Shroud.RevealedCells * 100d / world.Map.ProjectedCells.Length) / 100)
				.ToString("P0", NumberFormatInfo.CurrentInfo);
		}

		string AverageOrdersPerMinute(PlayerStatistics stats)
		{
			var orders = stats?.OrderCount ?? 0;
			return (world.WorldTick == 0 ? 0 : orders / (world.WorldTick / 1500.0))
				.ToString("F1", NumberFormatInfo.CurrentInfo);
		}

		(string Text, Color Color)[] StatValues(Player player, PlayerStatistics stats, Color playerColor)
		{
			var res = player.PlayerActor.TraitOrDefault<PlayerResources>();
			var pm = player.PlayerActor.TraitOrDefault<PowerManager>();

			var powerText = pm != null ? $"{pm.PowerDrained}/{pm.PowerProvided}" : "-";
			var powerColor = pm != null ? GetPowerColor(pm.PowerState) : playerColor;

			return
			[
				(Currency(res?.GetCashAndResources() ?? 0), playerColor),
				(Currency(stats?.DisplayIncome ?? 0), playerColor),
				(Currency(res?.Earned ?? 0), playerColor),
				(Currency(res?.Spent ?? 0), playerColor),
				(HarvesterCount(player).ToString(NumberFormatInfo.CurrentInfo), playerColor),
				(DerrickCount(player).ToString(NumberFormatInfo.CurrentInfo), playerColor),
				(powerText, powerColor),
				(Currency(stats?.ArmyValue ?? 0), playerColor),
				(Currency(stats?.AssetsValue ?? 0), playerColor),
				(stats != null ? (stats.UnitsKilled + stats.BuildingsKilled).ToString(NumberFormatInfo.CurrentInfo) : "-", playerColor),
				(stats != null ? (stats.UnitsDead + stats.BuildingsDead).ToString(NumberFormatInfo.CurrentInfo) : "-", playerColor),
				(stats != null ? $"{stats.UnitsKilled}/{stats.UnitsDead}" : "-", playerColor),
				(stats != null ? $"{stats.BuildingsKilled}/{stats.BuildingsDead}" : "-", playerColor),
				(Currency(stats?.KillsCost ?? 0), playerColor),
				(Currency(stats?.DeathsCost ?? 0), playerColor),
				(Vision(player), playerColor),
				((stats?.Experience ?? 0).ToString(NumberFormatInfo.CurrentInfo), playerColor),
				(AverageOrdersPerMinute(stats), playerColor)
			];
		}

		public override void Draw()
		{
			var p1 = GetPlayer1();
			var p2 = GetPlayer2();
			if (p1 == null || p2 == null)
				return;

			var stats1 = p1.PlayerActor.TraitOrDefault<PlayerStatistics>();
			var stats2 = p2.PlayerActor.TraitOrDefault<PlayerStatistics>();

			var rb = RenderBounds;
			var centerX = rb.X + rb.Width / 2;

			var nameFont = Game.Renderer.Fonts[NameFont];
			var labelFont = Game.Renderer.Fonts[LabelFont];

			// Player names and series scores centered around the middle.
			var p1Text = $"{p1.ResolvedPlayerName}  {GetPlayer1Score().ToString(CultureInfo.InvariantCulture)}";
			var p2Text = $"{GetPlayer2Score().ToString(CultureInfo.InvariantCulture)}  {p2.ResolvedPlayerName}";
			var p1Size = nameFont.Measure(p1Text);
			nameFont.DrawTextWithContrast(p1Text, new float2(centerX - 12 - p1Size.X, rb.Y), p1.Color, Color.Black, 1);
			nameFont.DrawTextWithContrast(p2Text, new float2(centerX + 12, rb.Y), p2.Color, Color.Black, 1);

			var y = rb.Y + HeaderHeight;
			DrawMetric(labelFont, centerX, y, "Units", UnitCount(stats1), UnitCount(stats2), p1.Color, p2.Color, false);
			y += BarHeight + BarSpacing;
			DrawMetric(labelFont, centerX, y, "Money", Money(p1), Money(p2), p1.Color, p2.Color, true);
			y += BarHeight + BarSpacing;
			DrawMetric(labelFont, centerX, y, "Assets", Assets(stats1), Assets(stats2), p1.Color, p2.Color, true);
			y += BarHeight + BarSpacing;
			DrawMetric(labelFont, centerX, y, "Power", Power(p1), Power(p2), p1.Color, p2.Color, false);

			if (ShowStats())
			{
				y += BarHeight + BarSpacing + 20;
				DrawStats(labelFont, centerX, y, p1, p2, stats1, stats2);
			}
		}

		void DrawStats(SpriteFont font, int centerX, int topY, Player p1, Player p2, PlayerStatistics stats1, PlayerStatistics stats2)
		{
			var cr = Game.Renderer.RgbaColorRenderer;

			var v1 = StatValues(p1, stats1, p1.Color);
			var v2 = StatValues(p2, stats2, p2.Color);

			var panelHeight = StatLabels.Length * StatLineHeight + 6;

			// Left column (player 1): labels on the outer edge, values toward the centre.
			var leftLabelX = centerX - 10 - StatColumnWidth;
			cr.FillRect(new float2(leftLabelX - 4, topY - 3), new float2(centerX - 6, topY + panelHeight), PanelColor);

			// Right column (player 2): labels near the centre, values on the outer edge.
			var rightLabelX = centerX + 10;
			cr.FillRect(new float2(centerX + 6, topY - 3), new float2(rightLabelX + StatColumnWidth + 4, topY + panelHeight), PanelColor);

			for (var i = 0; i < StatLabels.Length; i++)
			{
				var lineY = topY + i * StatLineHeight;

				// Player 1: label left-aligned, value right-aligned ending near the centre.
				font.DrawTextWithContrast(StatLabels[i], new float2(leftLabelX, lineY), Color.White, Color.Black, 1);
				var v1Size = font.Measure(v1[i].Text);
				font.DrawTextWithContrast(v1[i].Text, new float2(centerX - 10 - v1Size.X, lineY), v1[i].Color, Color.Black, 1);

				// Player 2: label left-aligned near the centre, value right-aligned on the outer edge.
				font.DrawTextWithContrast(StatLabels[i], new float2(rightLabelX, lineY), Color.White, Color.Black, 1);
				var v2Size = font.Measure(v2[i].Text);
				font.DrawTextWithContrast(v2[i].Text, new float2(rightLabelX + StatColumnWidth - v2Size.X, lineY), v2[i].Color, Color.Black, 1);
			}
		}

		void DrawMetric(SpriteFont font, int centerX, int y, string label, long v1, long v2, Color c1, Color c2, bool currency)
		{
			var cr = Game.Renderer.RgbaColorRenderer;

			// Background troughs for both sides.
			cr.FillRect(new float2(centerX - BarMaxWidth, y), new float2(centerX + BarMaxWidth, y + BarHeight), BackgroundColor);

			var total = v1 + v2;
			var frac1 = total > 0 ? (float)v1 / total : 0f;
			var frac2 = total > 0 ? (float)v2 / total : 0f;
			var w1 = (int)(frac1 * BarMaxWidth);
			var w2 = (int)(frac2 * BarMaxWidth);

			if (w1 > 0)
				cr.FillRect(new float2(centerX - w1, y), new float2(centerX, y + BarHeight), c1);
			if (w2 > 0)
				cr.FillRect(new float2(centerX, y), new float2(centerX + w2, y + BarHeight), c2);

			// Center divider.
			cr.FillRect(new float2(centerX - 1, y - 1), new float2(centerX + 1, y + BarHeight + 1), BorderColor);

			// Metric label in the middle of the trough.
			var labelSize = font.Measure(label);
			font.DrawTextWithContrast(label, new float2(centerX - labelSize.X / 2, y + (BarHeight - labelSize.Y) / 2 - 2),
				Color.White, Color.Black, 1);

			// Numeric values on the outer edges.
			var v1Text = FormatValue(v1, currency);
			var v2Text = FormatValue(v2, currency);
			var v1Size = font.Measure(v1Text);
			font.DrawTextWithContrast(v1Text, new float2(centerX - BarMaxWidth + 2, y + (BarHeight - v1Size.Y) / 2 - 2),
				Color.White, Color.Black, 1);
			var v2Size = font.Measure(v2Text);
			font.DrawTextWithContrast(v2Text, new float2(centerX + BarMaxWidth - 2 - v2Size.X, y + (BarHeight - v2Size.Y) / 2 - 2),
				Color.White, Color.Black, 1);
		}

		static string FormatValue(long value, bool currency)
		{
			var text = value.ToString(NumberFormatInfo.CurrentInfo);
			return currency ? "$" + text : text;
		}
	}
}
