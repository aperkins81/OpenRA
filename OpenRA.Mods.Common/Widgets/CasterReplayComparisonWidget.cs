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

		public int BarHeight = 18;
		public int BarVerticalGap = 3;
		public int NameRowHeight = 36;
		public int BarTopGap = 3;
		public int CenterTextGap = 12;
		public int CenterTextPadding = 6;
		public int LeftReservedWidth = 180;
		public int RightReservedWidth = 250;
		public int EdgePadding = 8;
		public int StatColumnWidth = 130;
		public int StatColumnGap = 6;
		public int StatLineHeight = 13;
		public int StatSectionHeadingHeight = 14;
		public string NameFont = "CasterComparisonValue";
		public string LabelFont = "TinyBold";
		public string ValueFont = "MediumBold";
		public long MoneyMax = 15000;
		public long AssetsMax = 50000;
		public long ArmyMax = 50000;
		public int ArmyIconInlineHeight = 48;
		public int ArmyIconTopOffset = -12;
		public int GraphRowHeight = 64;
		public int GraphRowGap = 4;
		public int GraphBottomMargin = 12;

		public readonly string ArmyTooltipTemplate = "ARMY_TOOLTIP";
		public readonly string BarTooltipTemplate = "SIMPLE_TOOLTIP";
		public readonly string TooltipContainer;

		public ArmyUnit TooltipUnit { get; private set; }
		public Func<ArmyUnit> GetTooltipUnit;

		static readonly Color BarFillColor = Color.FromArgb(115, 160, 190, 220);

		sealed class ValueBarHitRegion
		{
			public Rectangle Bounds;
			public string TooltipText;
		}

		static readonly string[] StatLabels =
		[
			"Cash", "Income", "Earned", "Spent", "Harvesters", "Derricks", "Power", "Army", "Assets",
			"Kills", "Deaths", "Units K/L", "Bldgs K/L", "Destroyed", "Lost", "Vision", "Exp", "APM"
		];

		static readonly (string Col1Heading, string Col2Heading, int Rows)[] StatSections =
		[
			("Economy", "Combat", 6),
			("Assets", "Intel", 3),
		];

		static readonly Color BackgroundColor = Color.FromArgb(90, 20, 20, 20);
		static readonly Color BorderColor = Color.FromArgb(200, 0, 0, 0);
		static readonly Color PanelColor = Color.FromArgb(180, 10, 10, 10);
		static readonly Color SectionHeadingColor = Color.FromArgb(255, 220, 200, 120);

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly List<ArmyIconsGridRenderer.ArmyIconHitRegion> armyIconHitRegions = [];
		readonly List<ValueBarHitRegion> valueBarHitRegions = [];
		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		int lastArmyIconIdx;
		int lastValueBarIdx = -1;
		int currentArmyTooltipToken;
		int currentBarTooltipToken;
		string barTooltipText;

		[ObjectCreator.UseCtor]
		public CasterReplayComparisonWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			GetTooltipUnit = () => TooltipUnit;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
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

		static long ArmyValue(PlayerStatistics stats)
		{
			return stats?.ArmyValue ?? 0;
		}

		static (long Used, long Provided, PowerState State) Power(Player player)
		{
			var pm = player.PlayerActor.TraitOrDefault<PowerManager>();
			return pm != null ? (pm.PowerDrained, pm.PowerProvided, pm.PowerState) : (0, 0, default);
		}

		static string Currency(long value)
		{
			return "$" + value.ToString(NumberFormatInfo.CurrentInfo);
		}

		static string FormatPower(long used, long provided)
		{
			return used.ToString(NumberFormatInfo.CurrentInfo) + "/" + provided.ToString(NumberFormatInfo.CurrentInfo);
		}

		static float PowerFraction(long used, long provided)
		{
			if (provided <= 0)
				return 0;

			return Math.Min(1f, Math.Max(0, (float)used / provided));
		}

		static float StaticFraction(long value, long max)
		{
			if (max <= 0)
				return 0;

			return Math.Min(1f, Math.Max(0, (float)value / max));
		}

		static int SelectedArmyValue(World world, Player player)
		{
			return world.Selection.Actors
				.Where(a => a.Owner == player && a.IsInWorld && !a.IsDead)
				.Sum(a => a.Info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0);
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

		static readonly Color StatValueColor = Color.White;

		(string Text, Color Color)[] StatValues(Player player, PlayerStatistics stats)
		{
			var res = player.PlayerActor.TraitOrDefault<PlayerResources>();
			var pm = player.PlayerActor.TraitOrDefault<PowerManager>();

			var powerText = pm != null ? $"{pm.PowerDrained}/{pm.PowerProvided}" : "-";
			var powerColor = pm != null ? GetPowerColor(pm.PowerState) : StatValueColor;

			return
			[
				(Currency(res?.GetCashAndResources() ?? 0), StatValueColor),
				(Currency(stats?.DisplayIncome ?? 0), StatValueColor),
				(Currency(res?.Earned ?? 0), StatValueColor),
				(Currency(res?.Spent ?? 0), StatValueColor),
				(HarvesterCount(player).ToString(NumberFormatInfo.CurrentInfo), StatValueColor),
				(DerrickCount(player).ToString(NumberFormatInfo.CurrentInfo), StatValueColor),
				(powerText, powerColor),
				(Currency(stats?.ArmyValue ?? 0), StatValueColor),
				(Currency(stats?.AssetsValue ?? 0), StatValueColor),
				(stats != null ? (stats.UnitsKilled + stats.BuildingsKilled).ToString(NumberFormatInfo.CurrentInfo) : "-", StatValueColor),
				(stats != null ? (stats.UnitsDead + stats.BuildingsDead).ToString(NumberFormatInfo.CurrentInfo) : "-", StatValueColor),
				(stats != null ? $"{stats.UnitsKilled}/{stats.UnitsDead}" : "-", StatValueColor),
				(stats != null ? $"{stats.BuildingsKilled}/{stats.BuildingsDead}" : "-", StatValueColor),
				(Currency(stats?.KillsCost ?? 0), StatValueColor),
				(Currency(stats?.DeathsCost ?? 0), StatValueColor),
				(Vision(player), StatValueColor),
				((stats?.Experience ?? 0).ToString(NumberFormatInfo.CurrentInfo), StatValueColor),
				(AverageOrdersPerMinute(stats), StatValueColor)
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
			var valueFont = Game.Renderer.Fonts[ValueFont];

			var army1 = ArmyValue(stats1);
			var army2 = ArmyValue(stats2);
			var money1 = Money(p1);
			var money2 = Money(p2);
			var assets1 = Assets(stats1);
			var assets2 = Assets(stats2);
			var (powerUsed1, powerProvided1, powerState1) = Power(p1);
			var (powerUsed2, powerProvided2, powerState2) = Power(p2);
			var powerText1 = FormatPower(powerUsed1, powerProvided1);
			var powerText2 = FormatPower(powerUsed2, powerProvided2);
			var selectedArmy1 = SelectedArmyValue(world, p1);
			var selectedArmy2 = SelectedArmyValue(world, p2);
			var gapHalf = ComputeCenterGapHalf(valueFont,
				FormatValue(army1, true), FormatValue(army2, true),
				FormatValue(money1, true), FormatValue(money2, true),
				FormatValue(assets1, true), FormatValue(assets2, true),
				powerText1, powerText2);

			var leftOuter = rb.X + LeftReservedWidth + EdgePadding;
			var rightOuter = rb.Right - RightReservedWidth - EdgePadding;
			var leftInner = centerX - gapHalf;
			var rightInner = centerX + gapHalf;
			var leftBarWidth = Math.Max(0, leftInner - leftOuter);
			var rightBarWidth = Math.Max(0, rightOuter - rightInner);

			var exp1 = stats1?.Experience ?? 0;
			var exp2 = stats2?.Experience ?? 0;
			var p1Text = $"{p1.ResolvedPlayerName} - {exp1.ToString(NumberFormatInfo.CurrentInfo)} - {GetPlayer1Score().ToString(CultureInfo.InvariantCulture)}";
			var p2Text = $"{GetPlayer2Score().ToString(CultureInfo.InvariantCulture)} - {exp2.ToString(NumberFormatInfo.CurrentInfo)} - {p2.ResolvedPlayerName}";
			var p1Size = nameFont.Measure(p1Text);
			var p2Size = nameFont.Measure(p2Text);
			var nameY = rb.Y + (NameRowHeight - nameFont.Measure("0").Y) / 2 - 2;
			nameFont.DrawTextWithContrast(p1Text, new float2(centerX - CenterTextGap - p1Size.X, nameY), Color.White, p1.Color, 1);
			nameFont.DrawTextWithContrast(p2Text, new float2(centerX + CenterTextGap, nameY), Color.White, p2.Color, 1);

			const int NameIconPad = 4;
			var countFont = Game.Renderer.Fonts[LabelFont];
			var p1IconWidth = Math.Max(0, centerX - CenterTextGap - p1Size.X - NameIconPad - leftOuter);
			var p2IconWidth = Math.Max(0, rightOuter - (centerX + CenterTextGap + p2Size.X) - NameIconPad);
			var armyIconY = rb.Y + ArmyIconTopOffset;
			var armyIconRowHeight = NameRowHeight - ArmyIconTopOffset;
			armyIconHitRegions.Clear();
			ArmyIconsGridRenderer.Draw(countFont, worldRenderer, p1, stats1, leftOuter, armyIconY,
				p1IconWidth, armyIconRowHeight, ArmyIconInlineHeight, hitRegions: armyIconHitRegions);
			ArmyIconsGridRenderer.Draw(countFont, worldRenderer, p2, stats2, rightOuter - p2IconWidth, armyIconY,
				p2IconWidth, armyIconRowHeight, ArmyIconInlineHeight, rightAligned: true,
				hitRegions: armyIconHitRegions);

			var y = rb.Y + NameRowHeight + BarTopGap;
			valueBarHitRegions.Clear();
			DrawValueBarRow(centerX, gapHalf, leftBarWidth, rightBarWidth, y, valueFont, "Army",
				StaticFraction(army1, ArmyMax), StaticFraction(army2, ArmyMax),
				FormatValue(army1, true), FormatValue(army2, true), p1.Color, p2.Color,
				p1.Color, p2.Color, p1.ResolvedPlayerName, p2.ResolvedPlayerName);
			y += BarHeight + BarVerticalGap;
			DrawValueBarRow(centerX, gapHalf, leftBarWidth, rightBarWidth, y, valueFont, "Money",
				StaticFraction(money1, MoneyMax), StaticFraction(money2, MoneyMax),
				FormatValue(money1, true), FormatValue(money2, true), p1.Color, p2.Color,
				p1.Color, p2.Color, p1.ResolvedPlayerName, p2.ResolvedPlayerName);
			y += BarHeight + BarVerticalGap;
			DrawValueBarRow(centerX, gapHalf, leftBarWidth, rightBarWidth, y, valueFont, "Assets",
				StaticFraction(assets1, AssetsMax), StaticFraction(assets2, AssetsMax),
				FormatValue(assets1, true), FormatValue(assets2, true), p1.Color, p2.Color,
				p1.Color, p2.Color, p1.ResolvedPlayerName, p2.ResolvedPlayerName);
			y += BarHeight + BarVerticalGap;
			DrawValueBarRow(centerX, gapHalf, leftBarWidth, rightBarWidth, y, valueFont, "Power",
				PowerFraction(powerUsed1, powerProvided1), PowerFraction(powerUsed2, powerProvided2),
				powerText1, powerText2, GetPowerColor(powerState1), GetPowerColor(powerState2),
				p1.Color, p2.Color, p1.ResolvedPlayerName, p2.ResolvedPlayerName);

			if (ShowStats())
			{
				y += BarHeight + 8;
				DrawStats(labelFont, centerX, y, p1, p2, stats1, stats2);
			}

			DrawCenteredGraphPanels(leftOuter, rightInner, leftBarWidth, rightBarWidth, p1, p2, stats1, stats2);
			DrawSelectionValues(valueFont, rb.X, p1, p2, selectedArmy1, selectedArmy2);
		}

		public override void Tick()
		{
			if (TooltipContainer == null)
				return;

			if (Ui.MouseOverWidget != this)
			{
				ClearArmyTooltip();
				ClearBarTooltip();
				return;
			}

			for (var i = 0; i < armyIconHitRegions.Count; i++)
			{
				if (!armyIconHitRegions[i].Bounds.Contains(Viewport.LastMousePos))
					continue;

				if (TooltipUnit == armyIconHitRegions[i].Unit && lastArmyIconIdx == i)
					return;

				ClearBarTooltip();
				lastArmyIconIdx = i;
				TooltipUnit = armyIconHitRegions[i].Unit;
				currentArmyTooltipToken = tooltipContainer.Value.SetTooltip(
					ArmyTooltipTemplate,
					new WidgetArgs { { "getTooltipUnit", GetTooltipUnit } });
				return;
			}

			ClearArmyTooltip();

			for (var i = 0; i < valueBarHitRegions.Count; i++)
			{
				if (!valueBarHitRegions[i].Bounds.Contains(Viewport.LastMousePos))
					continue;

				if (barTooltipText == valueBarHitRegions[i].TooltipText && lastValueBarIdx == i)
					return;

				ClearBarTooltip();
				lastValueBarIdx = i;
				barTooltipText = valueBarHitRegions[i].TooltipText;
				currentBarTooltipToken = tooltipContainer.Value.SetTooltip(
					BarTooltipTemplate,
					new WidgetArgs { { "getText", () => barTooltipText } });
				return;
			}

			ClearBarTooltip();
		}

		void ClearArmyTooltip()
		{
			if (TooltipUnit == null)
				return;

			tooltipContainer.Value.RemoveTooltip(currentArmyTooltipToken);
			lastArmyIconIdx = 0;
			TooltipUnit = null;
		}

		void ClearBarTooltip()
		{
			if (barTooltipText == null)
				return;

			tooltipContainer.Value.RemoveTooltip(currentBarTooltipToken);
			lastValueBarIdx = -1;
			barTooltipText = null;
		}

		int ComputeCenterGapHalf(SpriteFont font, params string[] values)
		{
			var maxWidth = values.Max(t => font.Measure(t).X);

			return (maxWidth * 2 + CenterTextGap + CenterTextPadding * 2) / 2;
		}

		void DrawStatCell(SpriteFont font, string label, (string Text, Color Color) value, int labelX, int lineY)
		{
			font.DrawTextWithContrast(label, new float2(labelX, lineY), Color.White, Color.Black, 1);
			var valueSize = font.Measure(value.Text);
			font.DrawTextWithContrast(value.Text, new float2(labelX + StatColumnWidth - valueSize.X, lineY), value.Color, Color.Black, 1);
		}

		void DrawStatCellMirrored(SpriteFont font, string label, (string Text, Color Color) value, int labelX, int lineY)
		{
			font.DrawTextWithContrast(value.Text, new float2(labelX, lineY), value.Color, Color.Black, 1);
			var labelSize = font.Measure(label);
			font.DrawTextWithContrast(label, new float2(labelX + StatColumnWidth - labelSize.X, lineY), Color.White, Color.Black, 1);
		}

		void DrawColumnSectionHeading(SpriteFont font, string heading, int columnX, int lineY)
		{
			var size = font.Measure(heading);
			font.DrawTextWithContrast(heading, new float2(columnX + (StatColumnWidth - size.X) / 2, lineY), SectionHeadingColor, Color.Black, 1);
		}

		static int StatPanelHeight(int statLineHeight, int sectionHeadingHeight)
		{
			var statRows = StatSections.Sum(s => s.Rows);
			var sectionCount = StatSections.Length;
			return sectionCount * sectionHeadingHeight + statRows * statLineHeight + 6;
		}

		void DrawStats(SpriteFont font, int centerX, int topY, Player p1, Player p2, PlayerStatistics stats1, PlayerStatistics stats2)
		{
			var cr = Game.Renderer.RgbaColorRenderer;

			var v1 = StatValues(p1, stats1);
			var v2 = StatValues(p2, stats2);

			const int CenterGap = 10;
			var panelHeight = StatPanelHeight(StatLineHeight, StatSectionHeadingHeight);

			// Player 1: outer column on the left, inner column toward the centre.
			var p1InnerLabelX = centerX - CenterGap - StatColumnWidth;
			var p1OuterLabelX = p1InnerLabelX - StatColumnGap - StatColumnWidth;
			cr.FillRect(new float2(p1OuterLabelX - 4, topY - 3), new float2(centerX - 6, topY + panelHeight), PanelColor);

			// Player 2: inner column toward the centre, outer column on the right.
			var p2InnerLabelX = centerX + CenterGap;
			var p2OuterLabelX = p2InnerLabelX + StatColumnWidth + StatColumnGap;
			cr.FillRect(new float2(centerX + 6, topY - 3), new float2(p2OuterLabelX + StatColumnWidth + 4, topY + panelHeight), PanelColor);

			var lineY = topY;
			var statRow = 0;
			foreach (var (col1Heading, col2Heading, rows) in StatSections)
			{
				DrawColumnSectionHeading(font, col2Heading, p1OuterLabelX, lineY);
				DrawColumnSectionHeading(font, col1Heading, p1InnerLabelX, lineY);
				DrawColumnSectionHeading(font, col1Heading, p2InnerLabelX, lineY);
				DrawColumnSectionHeading(font, col2Heading, p2OuterLabelX, lineY);
				lineY += StatSectionHeadingHeight;

				for (var i = 0; i < rows; i++, statRow++)
				{
					var half = StatLabels.Length / 2;
					DrawStatCellMirrored(font, StatLabels[statRow + half], v1[statRow + half], p1OuterLabelX, lineY);
					DrawStatCellMirrored(font, StatLabels[statRow], v1[statRow], p1InnerLabelX, lineY);

					DrawStatCell(font, StatLabels[statRow], v2[statRow], p2InnerLabelX, lineY);
					DrawStatCell(font, StatLabels[statRow + StatLabels.Length / 2], v2[statRow + StatLabels.Length / 2], p2OuterLabelX, lineY);

					lineY += StatLineHeight;
				}
			}
		}

		// Draws the value of the currently selected units in the bottom-left corner,
		// Combined Arms style, one line per player that has units selected.
		void DrawSelectionValues(SpriteFont font, int leftX, Player p1, Player p2, int selected1, int selected2)
		{
			var entries = new List<(Player Player, int Value)>();
			if (selected1 > 0)
				entries.Add((p1, selected1));

			if (selected2 > 0)
				entries.Add((p2, selected2));

			if (entries.Count == 0)
				return;

			var cr = Game.Renderer.RgbaColorRenderer;
			var left = leftX + 12;
			var lineHeight = font.Measure("0").Y + 8;
			var bottom = Game.Renderer.Resolution.Height - 12;

			for (var i = 0; i < entries.Count; i++)
			{
				var (player, value) = entries[entries.Count - 1 - i];
				var text = "Selected: " + Currency(value);
				var size = font.Measure(text);
				var lineY = bottom - (i + 1) * lineHeight;

				var topLeft = new float2(left - 6, lineY - 4);
				var bottomRight = new float2(left + size.X + 6, lineY + size.Y + 4);
				cr.FillRect(topLeft, bottomRight, BackgroundColor);

				// Player-colored border.
				cr.FillRect(topLeft, new float2(bottomRight.X, topLeft.Y + 1), player.Color);
				cr.FillRect(new float2(topLeft.X, bottomRight.Y - 1), bottomRight, player.Color);
				cr.FillRect(topLeft, new float2(topLeft.X + 1, bottomRight.Y), player.Color);
				cr.FillRect(new float2(bottomRight.X - 1, topLeft.Y), bottomRight, player.Color);

				font.DrawTextWithContrast(text, new float2(left, lineY), Color.White, Color.Black, 1);
			}
		}

		void DrawCenteredGraphPanels(int leftOuter, int rightInner, int leftBarWidth, int rightBarWidth,
			Player p1, Player p2, PlayerStatistics stats1, PlayerStatistics stats2)
		{
			var screenH = Game.Renderer.Resolution.Height;
			var panelH = GraphRowHeight * 2 + GraphRowGap;
			var top = screenH - GraphBottomMargin - panelH;
			var labelFont = Game.Renderer.Fonts[LabelFont];

			if (leftBarWidth > 0)
				DrawComparisonGraphPanel(leftOuter, top, leftBarWidth, stats1?.ArmySamples, stats2?.ArmySamples,
					p1.ResolvedPlayerName, p2.ResolvedPlayerName, p1.Color, p2.Color, "Army", labelFont);

			if (rightBarWidth > 0)
				DrawComparisonGraphPanel(rightInner, top, rightBarWidth, stats1?.IncomeSamples, stats2?.IncomeSamples,
					p1.ResolvedPlayerName, p2.ResolvedPlayerName, p1.Color, p2.Color, "Money", labelFont);
		}

		void DrawComparisonGraphPanel(int left, int top, int width,
			IReadOnlyList<int> samples1, IReadOnlyList<int> samples2,
			string player1Name, string player2Name, Color color1, Color color2,
			string metricTitle, SpriteFont labelFont)
		{
			var cr = Game.Renderer.RgbaColorRenderer;
			var panelH = GraphRowHeight * 2 + GraphRowGap;
			ComparisonSampleGraphRenderer.DrawComparison(cr, labelFont, left, top, width, panelH,
				samples1, samples2, player1Name, player2Name, color1, color2, metricTitle);
		}

		void DrawValueBarRow(int centerX, int gapHalf, int leftBarWidth, int rightBarWidth, int y, SpriteFont font, string label,
			float frac1, float frac2, string v1Text, string v2Text, Color c1, Color c2,
			Color player1Color, Color player2Color, string player1Name, string player2Name)
		{
			var cr = Game.Renderer.RgbaColorRenderer;
			var leftInner = centerX - gapHalf;
			var rightInner = centerX + gapHalf;

			cr.FillRect(new float2(leftInner - leftBarWidth, y), new float2(leftInner, y + BarHeight), BackgroundColor);
			cr.FillRect(new float2(rightInner, y), new float2(rightInner + rightBarWidth, y + BarHeight), BackgroundColor);

			DrawFill(cr, leftInner, y, -1, frac1, leftBarWidth, player1Color);
			DrawFill(cr, rightInner, y, 1, frac2, rightBarWidth, player2Color);

			cr.FillRect(new float2(leftInner - 1, y - 1), new float2(leftInner + 1, y + BarHeight + 1), BorderColor);
			cr.FillRect(new float2(rightInner - 1, y - 1), new float2(rightInner + 1, y + BarHeight + 1), BorderColor);

			var v1Size = font.Measure(v1Text);
			var textY = y + (BarHeight - font.Measure("0").Y) / 2 - 2;

			font.DrawTextWithContrast(v1Text, new float2(centerX - CenterTextGap - v1Size.X, textY), Color.White, c1, 1);
			font.DrawTextWithContrast(v2Text, new float2(centerX + CenterTextGap, textY), Color.White, c2, 1);

			if (leftBarWidth > 0 || rightBarWidth > 0)
			{
				var rowLeft = leftInner - leftBarWidth;
				var rowWidth = rightInner + rightBarWidth - rowLeft;
				valueBarHitRegions.Add(new ValueBarHitRegion
				{
					Bounds = new Rectangle(rowLeft, y, rowWidth, BarHeight),
					TooltipText = $"{label}\n{player1Name}: {v1Text}\n{player2Name}: {v2Text}"
				});
			}
		}

		void DrawFill(RgbaColorRenderer cr, int innerEdge, int y, int sign, float frac, int barWidth, Color playerColor)
		{
			if (barWidth <= 0)
				return;

			DrawSpan(cr, innerEdge, y, sign, 0, frac, barWidth, BarFillColor);
			DrawSpanBorder(cr, innerEdge, y, sign, 0, frac, barWidth, playerColor);
		}

		void DrawSpan(RgbaColorRenderer cr, int innerEdge, int y, int sign, float fromFrac, float toFrac, int barWidth, Color color)
		{
			var from = (int)(fromFrac * barWidth);
			var to = (int)(toFrac * barWidth);
			if (to <= from)
				return;

			if (sign < 0)
				cr.FillRect(new float2(innerEdge - to, y), new float2(innerEdge - from, y + BarHeight), color);
			else
				cr.FillRect(new float2(innerEdge + from, y), new float2(innerEdge + to, y + BarHeight), color);
		}

		void DrawSpanBorder(RgbaColorRenderer cr, int innerEdge, int y, int sign, float fromFrac, float toFrac, int barWidth, Color color)
		{
			var from = (int)(fromFrac * barWidth);
			var to = (int)(toFrac * barWidth);
			if (to <= from)
				return;

			var x1 = sign < 0 ? innerEdge - to : innerEdge + from;
			var x2 = sign < 0 ? innerEdge - from : innerEdge + to;

			cr.FillRect(new float2(x1, y), new float2(x2, y + 1), color);
			cr.FillRect(new float2(x1, y + BarHeight - 1), new float2(x2, y + BarHeight), color);
			cr.FillRect(new float2(x1, y), new float2(x1 + 1, y + BarHeight), color);
			cr.FillRect(new float2(x2 - 1, y), new float2(x2, y + BarHeight), color);
		}

		static string FormatValue(long value, bool currency)
		{
			var text = value.ToString(NumberFormatInfo.CurrentInfo);
			return currency ? "$" + text : text;
		}
	}
}
