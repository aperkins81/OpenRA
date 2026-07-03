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

			var comparison = widget.Get<CasterReplayComparisonWidget>("COMPARISON");
			comparison.GetPlayer1 = () => p1;
			comparison.GetPlayer2 = () => p2;
			comparison.GetPlayer1Score = () => mode.Player1Score;
			comparison.GetPlayer2Score = () => mode.Player2Score;
			comparison.ShowStats = () => statsExpanded;

			var statsToggle = widget.Get<ButtonWidget>("STATS_TOGGLE");
			statsToggle.GetText = () => statsExpanded ? "-" : "+";
			statsToggle.OnClick = () => statsExpanded = !statsExpanded;

			var buildLeft = widget.Get<CasterReplayBuildColumnWidget>("BUILD_LEFT");
			buildLeft.GetPlayer = () => p1;

			var buildRight = widget.Get<CasterReplayBuildColumnWidget>("BUILD_RIGHT");
			buildRight.GetPlayer = () => p2;
		}
	}
}
