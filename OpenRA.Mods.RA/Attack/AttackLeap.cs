﻿#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.RA.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class AttackLeapInfo : AttackFrontalInfo
	{
		public override object Create(ActorInitializer init) { return new AttackLeap(init.self, this); }
	}

	class AttackLeap : AttackFrontal, ISync
	{
		public AttackLeap(Actor self, AttackLeapInfo info)
			: base(self, info) {}

		public override void DoAttack(Actor self, Target target)
		{
			if (!CanAttack(self, target))
				return;

			var a = ChooseArmamentForTarget(target);
			if (a == null)
				return;

			// TODO: Define weapon ranges as WRange
			var range = new WRange((int)(1024*a.Weapon.Range));
			if (!target.IsInRange(self.CenterPosition, range))
				return;

			self.CancelActivity();
			self.QueueActivity(new Leap(self, target));
		}
	}
}
