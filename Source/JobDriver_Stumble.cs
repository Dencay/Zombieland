﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace ZombieLand
{
	public class JobDriver_Stumble : JobDriver
	{
		public IntVec3 destination;

		public Thing eatTarget;
		public Pawn lastEatTarget;
		public int eatDelayCounter;
		public int eatDelay;

		void InitAction()
		{
			destination = IntVec3.Invalid;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref destination, "destination", IntVec3.Invalid);
			Scribe_References.Look(ref eatTarget, "eatTarget");
			Scribe_References.Look(ref lastEatTarget, "lastEatTarget");
			Scribe_Values.Look(ref eatDelayCounter, "eatDelayCounter");

			// previous versions of Zombieland stored the inner pawn of a corpse
			// in the eatTarget. We have since then changed it to contain the corpse
			// itself. For older saves, we need to convert this.
			//
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (eatTarget is Pawn p && p.Map != null)
				{
					// find corpse that points to the pawn we stored
					eatTarget = p.Map.thingGrid
						.ThingsListAt(eatTarget.Position)
						.OfType<Corpse>()
						.FirstOrDefault(c => c.InnerPawn == eatTarget);
				}
			}
		}

		//int ticker = 0;
		void TickAction()
		{
			var zombie = (Zombie)pawn;
			if (zombie.state == ZombieState.Emerging || zombie.state == ZombieState.Floating) return;

			/*
			// for debugging - let zombies only live for 600 ticks
			// --------------------------------------------------
			if (++ticker > 600)
			{
				EndJobWith(JobCondition.InterruptForced);
				zombie.Kill(null); return;
			}
			// --------------------------------------------------
			*/

			ZombieStateHandler.CheckEndRage(zombie);

			if (this.ShouldDie(zombie))
				return;

			if (this.Roping(zombie))
			{
				this.ExecuteMove(zombie, zombie.Map.GetGrid());
				return;
			}

			if (ZombieStateHandler.DownedOrUnconsciousness(zombie))
				return;

			if (this.Attack(zombie))
				return;

			if (this.ValidDestination(zombie))
				return;

			ZombieStateHandler.ApplyFire(zombie);

			var bodyType = zombie.story.bodyType;
			if (zombie.isMiner && (bodyType == BodyTypeDefOf.Fat || bodyType == BodyTypeDefOf.Hulk))
				if (this.Mine(zombie, true))
					return;

			var grid = zombie.Map.GetGrid();
			if (this.Eat(zombie, grid))
				return;

			bool smashTime;
			if (zombie.IsTanky)
			{
				if (this.Smash(zombie, true, false))
					return;
				smashTime = true;
			}
			else
			{
				smashTime = this.Track(zombie, grid);
				if (smashTime)
				{
					if (zombie.checkSmashable == false) smashTime = false;
					zombie.checkSmashable = false;
				}
				if (this.Smash(zombie, smashTime, true))
					return;
			}

			var possibleMoves = this.PossibleMoves(zombie);
			if (possibleMoves.Count > 0)
			{
				if (zombie.raging > 0 || zombie.IsTanky || zombie.isAlbino || zombie.isDarkSlimer || (zombie.wasMapPawnBefore && zombie.state != ZombieState.Tracking))
					if (this.RageMove(zombie, grid, possibleMoves, smashTime))
						return;

				if (zombie.raging <= 0)
				{
					if (zombie.isMiner)
						if (this.Mine(zombie, false))
							return;

					this.Wander(zombie, grid, possibleMoves);
				}
			}

			this.ExecuteMove(zombie, grid);

			ZombieStateHandler.BeginRage(zombie, grid);
		}

		public override void Notify_PatherArrived()
		{
			base.Notify_PatherArrived();
			destination = IntVec3.Invalid;

			var zombie = (Zombie)pawn;
			zombie.checkSmashable = true;

			if (zombie.IsActiveElectric)
				ZombieStateHandler.Electrify(zombie);
		}

		public override string GetReport()
		{
			return "Stumbling";
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			yield return new Toil()
			{
				initAction = new Action(InitAction),
				tickAction = new Action(TickAction),
				defaultCompleteMode = ToilCompleteMode.Never
			};
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}
	}
}
