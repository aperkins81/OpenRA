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

using OpenRA;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class CasterReplayOverlayLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public CasterReplayOverlayLogic(Widget widget, World world)
		{
			var mode = world.WorldActor.TraitOrDefault<CasterReplayMode>();
			var players = CasterReplayMode.GetPlayers(world);
			var enabled = mode != null && mode.Enabled && players.Length == 2;

			widget.IsVisible = () => enabled;
			if (!enabled)
				return;

			var p1 = players[0];
			var p2 = players[1];

			var statsExpanded = false;
			var overlayHidden = false;

			var comparison = widget.Get<CasterReplayComparisonWidget>("COMPARISON");
			comparison.GetPlayer1 = () => p1;
			comparison.GetPlayer2 = () => p2;
			comparison.GetPlayer1Score = () => mode.Player1Score;
			comparison.GetPlayer2Score = () => mode.Player2Score;
			comparison.ShowStats = () => statsExpanded;
			comparison.IsVisible = () => !overlayHidden;

			var statsToggle = widget.Get<ButtonWidget>("STATS_TOGGLE");
			statsToggle.GetText = () => "+";
			statsToggle.OnClick = () => statsExpanded = !statsExpanded;
			statsToggle.IsVisible = () => !overlayHidden;

			var hideToggle = widget.Get<ButtonWidget>("OVERLAY_HIDE_TOGGLE");
			hideToggle.GetText = () => "-";
			hideToggle.IsHighlighted = () => overlayHidden;
			hideToggle.OnClick = () =>
			{
				overlayHidden = !overlayHidden;
				if (overlayHidden)
					statsExpanded = false;
			};

			var statusBarsToggle = widget.Get<ButtonWidget>("STATUS_BARS_TOGGLE");
			statusBarsToggle.GetText = () => Game.Settings.Game.StatusBars switch
			{
				StatusBarsType.AlwaysShow => "HP",
				StatusBarsType.DamageShow => "DMG",
				_ => "OFF"
			};
			statusBarsToggle.IsHighlighted = () => Game.Settings.Game.StatusBars != StatusBarsType.Standard;
			statusBarsToggle.IsVisible = () => !overlayHidden;
			statusBarsToggle.OnClick = () =>
			{
				Game.Settings.Game.StatusBars = Game.Settings.Game.StatusBars switch
				{
					StatusBarsType.AlwaysShow => StatusBarsType.DamageShow,
					StatusBarsType.DamageShow => StatusBarsType.Standard,
					_ => StatusBarsType.AlwaysShow
				};
			};

			var waypointsToggle = widget.Get<ButtonWidget>("WAYPOINTS_TOGGLE");
			waypointsToggle.GetText = () => "↗";
			waypointsToggle.IsHighlighted = () => mode.ShowWaypointLines;
			waypointsToggle.OnClick = () => mode.ShowWaypointLines = !mode.ShowWaypointLines;
			waypointsToggle.IsVisible = () => !overlayHidden;

			var buildLeft = widget.Get<CasterReplayBuildColumnWidget>("BUILD_LEFT");
			buildLeft.GetPlayer = () => p1;
			buildLeft.IsVisible = () => !overlayHidden;

			var buildRight = widget.Get<CasterReplayBuildColumnWidget>("BUILD_RIGHT");
			buildRight.GetPlayer = () => p2;
			buildRight.IsVisible = () => !overlayHidden;

			var livestreamReserve = widget.Get<ContainerWidget>("CASTER_LIVESTREAM_RESERVE");
			livestreamReserve.IsVisible = () => !overlayHidden;
		}
	}
}
