#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free
 * software. It is made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets
{
	public static class ComparisonSampleGraphRenderer
	{
		const int TrailingSampleCount = 40;
		const int AxisPadding = 3;
		const int LegendGap = 8;
		static readonly Color AxisColor = Color.FromArgb(180, 255, 255, 255);

		public static void DrawComparison(RgbaColorRenderer cr, SpriteFont labelFont,
			int x, int y, int width, int height,
			IReadOnlyList<int> samples1, IReadOnlyList<int> samples2,
			string player1Name, string player2Name,
			Color color1, Color color2,
			string metricTitle = "Army")
		{
			if (width < 20 || height < 20)
				return;

			var scaledMax = Math.Max(ScaledMax(samples1), ScaledMax(samples2));
			var minLabel = FormatCurrency(0);
			var maxLabel = FormatCurrency((int)scaledMax);

			var labelHeight = labelFont.Measure("0").Y;
			var headerHeight = labelHeight + AxisPadding;
			var leftGutter = Math.Max(labelFont.Measure(maxLabel).X, labelFont.Measure(metricTitle).X) + AxisPadding * 2;

			var plotX = x + leftGutter;
			var plotWidth = width - leftGutter - AxisPadding;
			var plotHeight = height - headerHeight - AxisPadding;

			if (plotWidth < 2 || plotHeight < 2)
				return;

			var plotY = y + headerHeight;
			var plotBottom = plotY + plotHeight;

			labelFont.DrawTextWithContrast(metricTitle,
				new float2(plotX, y),
				Color.White, AxisColor, 1);

			var legendX = plotX + plotWidth;
			var p2Size = labelFont.Measure(player2Name);
			labelFont.DrawTextWithContrast(player2Name,
				new float2(legendX - p2Size.X, y),
				Color.White, color2, 1);
			legendX -= p2Size.X + LegendGap;

			var p1Size = labelFont.Measure(player1Name);
			labelFont.DrawTextWithContrast(player1Name,
				new float2(legendX - p1Size.X, y),
				Color.White, color1, 1);

			var (lastPoint1, lastValue1) = DrawSeries(cr, plotX, plotY, plotWidth, plotHeight, samples1, scaledMax, color1);
			var (lastPoint2, lastValue2) = DrawSeries(cr, plotX, plotY, plotWidth, plotHeight, samples2, scaledMax, color2);

			var leftAxisX = plotX;
			cr.DrawLine(new float2(leftAxisX, plotY), new float2(leftAxisX, plotBottom), 1, AxisColor);
			cr.DrawLine(new float2(leftAxisX, plotBottom), new float2(plotX + plotWidth, plotBottom), 1, AxisColor);

			DrawTickLabel(labelFont, maxLabel, leftAxisX, plotY, AxisColor, rightAligned: true);
			DrawTickLabel(labelFont, minLabel, leftAxisX, plotBottom - labelHeight, AxisColor, rightAligned: true);

			Game.Renderer.RgbaColorRenderer.DrawLine(
				new float2(leftAxisX - 4, plotY), new float2(leftAxisX, plotY), 1, AxisColor);
			Game.Renderer.RgbaColorRenderer.DrawLine(
				new float2(leftAxisX - 4, plotBottom), new float2(leftAxisX, plotBottom), 1, AxisColor);

			DrawEndLabel(labelFont, lastPoint1, lastValue1, color1);
			DrawEndLabel(labelFont, lastPoint2, lastValue2, color2);
		}

		public static void DrawDualAxis(RgbaColorRenderer cr, SpriteFont labelFont,
			int x, int y, int width, int height,
			IReadOnlyList<int> armySamples, IReadOnlyList<int> moneySamples,
			Color armyColor, Color moneyColor,
			string leftAxisLabel = "Army", string rightAxisLabel = "Money")
		{
			if (width < 20 || height < 20)
				return;

			var armyMax = ScaledMax(armySamples);
			var moneyMax = ScaledMax(moneySamples);

			var minLabel = FormatCurrency(0);
			var armyMaxLabel = FormatCurrency((int)armyMax);
			var moneyMaxLabel = FormatCurrency((int)moneyMax);

			var leftGutter = Math.Max(labelFont.Measure(leftAxisLabel).X, labelFont.Measure(armyMaxLabel).X) + AxisPadding * 2;
			var rightGutter = Math.Max(labelFont.Measure(rightAxisLabel).X, labelFont.Measure(moneyMaxLabel).X) + AxisPadding * 2;

			var plotX = x + leftGutter;
			var plotWidth = width - leftGutter - rightGutter;
			var plotHeight = height - labelFont.Measure("0").Y - AxisPadding;

			if (plotWidth < 2 || plotHeight < 2)
				return;

			var plotY = y;
			var plotBottom = plotY + plotHeight;

			DrawSeries(cr, plotX, plotY, plotWidth, plotHeight, armySamples, armyMax, armyColor);
			DrawSeries(cr, plotX, plotY, plotWidth, plotHeight, moneySamples, moneyMax, moneyColor);

			var leftAxisX = plotX;
			var rightAxisX = plotX + plotWidth;

			cr.DrawLine(new float2(leftAxisX, plotY), new float2(leftAxisX, plotBottom), 1, AxisColor);
			cr.DrawLine(new float2(rightAxisX, plotY), new float2(rightAxisX, plotBottom), 1, AxisColor);
			cr.DrawLine(new float2(leftAxisX, plotBottom), new float2(rightAxisX, plotBottom), 1, AxisColor);

			DrawLeftAxisLabels(labelFont, leftAxisX, plotY, plotBottom, armyColor, leftAxisLabel, minLabel, armyMaxLabel);
			DrawRightAxisLabels(labelFont, rightAxisX, plotY, plotBottom, moneyColor, rightAxisLabel, minLabel, moneyMaxLabel);
		}

		static (float3? LastPoint, int LastValue) DrawSeries(RgbaColorRenderer cr, int plotX, int plotY, int plotWidth, int plotHeight,
			IReadOnlyList<int> samples, float scaledMax, Color color, int lineWidth = 1)
		{
			if (samples == null || samples.Count < 2)
				return (null, 0);

			var scale = (plotHeight - 2) / scaledMax;
			var visibleCount = Math.Min(samples.Count, Math.Max(2, Math.Min(TrailingSampleCount, plotWidth / 2)));
			var start = samples.Count - visibleCount;

			var clipRect = new Rectangle(plotX, plotY, plotWidth, plotHeight);
			Game.Renderer.EnableScissor(clipRect);

			var points = new List<float3>(visibleCount);
			for (var i = 0; i < visibleCount; i++)
			{
				var sampleX = plotX + (int)((long)i * (plotWidth - 1) / (visibleCount - 1));
				var value = samples[start + i];
				var sampleY = plotY + plotHeight - 1 - (int)(value * scale);
				sampleY = Math.Max(plotY + 1, Math.Min(plotY + plotHeight - 1, sampleY));
				points.Add(new float3(sampleX, sampleY, 0));
			}

			if (points.Count > 1)
				cr.DrawLine(points, lineWidth, color);

			Game.Renderer.DisableScissor();

			var lastValue = samples[start + visibleCount - 1];
			return (points[^1], lastValue);
		}

		static void DrawEndLabel(SpriteFont font, float3? lastPoint, int lastValue, Color seriesColor)
		{
			if (lastPoint == null || lastValue == 0)
				return;

			var text = FormatCurrency(lastValue);
			var size = font.Measure(text);
			var pos = new float2(lastPoint.Value.X - size.X / 2, lastPoint.Value.Y - size.Y - 2);
			font.DrawTextWithContrast(text, pos, Color.White, seriesColor, 1);
		}

		static void DrawLeftAxisLabels(SpriteFont font, int axisX, int plotY, int plotBottom,
			Color axisColor, string axisLabel, string minLabel, string maxLabel)
		{
			var labelHeight = font.Measure("0").Y;

			font.DrawTextWithContrast(axisLabel,
				new float2(axisX - AxisPadding - font.Measure(axisLabel).X, plotY - labelHeight),
				Color.White, axisColor, 1);

			DrawTickLabel(font, maxLabel, axisX, plotY, axisColor, rightAligned: true);
			DrawTickLabel(font, minLabel, axisX, plotBottom - labelHeight, axisColor, rightAligned: true);

			Game.Renderer.RgbaColorRenderer.DrawLine(
				new float2(axisX - 4, plotY), new float2(axisX, plotY), 1, AxisColor);
			Game.Renderer.RgbaColorRenderer.DrawLine(
				new float2(axisX - 4, plotBottom), new float2(axisX, plotBottom), 1, AxisColor);
		}

		static void DrawRightAxisLabels(SpriteFont font, int axisX, int plotY, int plotBottom,
			Color axisColor, string axisLabel, string minLabel, string maxLabel)
		{
			var labelHeight = font.Measure("0").Y;

			font.DrawTextWithContrast(axisLabel,
				new float2(axisX + AxisPadding, plotY - labelHeight),
				Color.White, axisColor, 1);

			DrawTickLabel(font, maxLabel, axisX, plotY, axisColor, rightAligned: false);
			DrawTickLabel(font, minLabel, axisX, plotBottom - labelHeight, axisColor, rightAligned: false);

			Game.Renderer.RgbaColorRenderer.DrawLine(
				new float2(axisX, plotY), new float2(axisX + 4, plotY), 1, AxisColor);
			Game.Renderer.RgbaColorRenderer.DrawLine(
				new float2(axisX, plotBottom), new float2(axisX + 4, plotBottom), 1, AxisColor);
		}

		static void DrawTickLabel(SpriteFont font, string text, int axisX, int y, Color axisColor, bool rightAligned)
		{
			var size = font.Measure(text);
			var x = rightAligned ? axisX - AxisPadding - size.X : axisX + AxisPadding;
			font.DrawTextWithContrast(text, new float2(x, y), Color.White, axisColor, 1);
		}

		static float ScaledMax(IReadOnlyList<int> samples)
		{
			if (samples == null || samples.Count == 0)
				return 5000f;

			var maxValue = 0f;
			for (var i = 0; i < samples.Count; i++)
				maxValue = Math.Max(maxValue, samples[i]);

			return Math.Max((float)Math.Ceiling(maxValue / 1000) * 1000, 5000f);
		}

		static string FormatCurrency(int value)
		{
			return "$" + value.ToString(NumberFormatInfo.CurrentInfo);
		}
	}
}
