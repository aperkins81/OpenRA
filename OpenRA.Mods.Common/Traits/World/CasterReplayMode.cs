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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Provides caster overlay support for two-player replays configured from the replay viewer.",
		"Shows mirrored comparison bars and per-queue build progress when watching the replay.")]
	public class CasterReplayModeInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new CasterReplayMode(); }
	}

	public class CasterReplayMode : INotifyCreated
	{
		const int MinScore = 0;
		const int MaxScore = 9;

		public bool Enabled { get; private set; }
		public int Player1Score { get; private set; }
		public int Player2Score { get; private set; }

		void INotifyCreated.Created(Actor self)
		{
			if (!self.World.IsReplay)
			{
				Enabled = false;
				return;
			}

			var settings = Game.Settings.Game;
			Enabled = settings.ReplayCasterOverlayEnabled;
			Player1Score = ClampScore(settings.ReplayCasterP1Score);
			Player2Score = ClampScore(settings.ReplayCasterP2Score);
		}

		static int ClampScore(int score)
		{
			return Math.Clamp(score, MinScore, MaxScore);
		}

		// Returns the (up to two) combatant players used for the caster overlay,
		// ordered deterministically by spawn point then client index so the same
		// player is always shown on the same side.
		public static Player[] GetPlayers(World world)
		{
			return world.Players
				.Where(p => p.Playable && !p.NonCombatant)
				.OrderBy(p => p.SpawnPoint)
				.ThenBy(p => p.ClientIndex)
				.Take(2)
				.ToArray();
		}
	}
}
