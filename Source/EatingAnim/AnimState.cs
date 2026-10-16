using System;
using System.Collections.Concurrent;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EatingAnim
{
    /// <summary>What a pawn is animating right now. Computed on demand, never stored.</summary>
    public struct AnimState
    {
        public AnimKind kind;
        public float t;          // animation clock in ticks (speed scaled, offset per pawn)
        public float progress;   // 0..1 for progress-driven animations, -1 when unknown
        public Rot4 rot;
        public Job job;
        public JobDriver driver;
        public Thing subject;    // the real in-game thing involved: food, book, apparel, weapon...
        public IntVec3 cell;     // table cell for eating
    }

    public static class AnimStateUtility
    {
        private static readonly Func<JobDriver, Toil> curToil =
            AccessTools.MethodDelegate<Func<JobDriver, Toil>>(AccessTools.PropertyGetter(typeof(JobDriver), "CurToil"));
        private static readonly AccessTools.FieldRef<JobDriver_Ingest, Toil> chewingToil =
            AccessTools.FieldRefAccess<JobDriver_Ingest, Toil>("chewing");
        private static readonly AccessTools.FieldRef<JobDriver_Wear, int> wearDuration =
            AccessTools.FieldRefAccess<JobDriver_Wear, int>("duration");
        private static readonly AccessTools.FieldRef<JobDriver_RemoveApparel, int> unwearDuration =
            AccessTools.FieldRefAccess<JobDriver_RemoveApparel, int>("duration");

        // A large multiple of every cycle length would be ideal; any jump here happens once per ~55 in-game days.
        private const int ClockWrap = 3_326_400;

        private static readonly TargetIndex tableCellInd = ReadStatic(typeof(JobDriver_Ingest), "TableCellInd", TargetIndex.B);

        private static TargetIndex ReadStatic(Type type, string name, TargetIndex fallback)
        {
            try
            {
                var f = AccessTools.Field(type, name);
                if (f != null) return (TargetIndex)f.GetValue(null);
            }
            catch (Exception e)
            {
                Log.Warning($"[EatingAnim] Could not read {type.Name}.{name}: {e.Message}");
            }
            return fallback;
        }

        /// <summary>
        /// Decides whether the pawn should animate and how. Pure reads of the pawn's current job, so
        /// it is safe from the game's parallel pre-draw threads and costs a handful of field reads.
        /// </summary>
        public static bool TryGet(Pawn pawn, out AnimState s)
        {
            s = default;
            var settings = EatingAnimMod.Settings;
            if (!settings.enabled || !AnimClassifier.Ready || pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
                return false;
            // Cheapest rejections first: most pawns on screen are walking or idle.
            if (pawn.pather != null && pawn.pather.Moving)
                return false;

            bool humanlike = pawn.RaceProps.Humanlike;
            int now = GenTicks.TicksGame;
            s.rot = pawn.Rotation;
            // Wrapped in integer space so the float clock never loses precision on long games.
            s.t = ((now + (pawn.thingIDNumber * 97) % 997) % ClockWrap) * settings.speed;
            s.progress = -1f;

            if (humanlike && EquipFlourish.TryGet(pawn, now, out var eq, out float eqProgress))
            {
                s.kind = AnimKind.Equip;
                s.subject = eq;
                s.progress = eqProgress;
                return settings.IsOn(AnimCategory.Equip) && CanAnimate(pawn);
            }

            var driver = pawn.jobs?.curDriver;
            var job = driver?.job;
            if (job?.def == null)
                return false;
            var modes = AnimClassifier.jobModes;
            int index = job.def.index;
            if (index >= modes.Length)
                return false;
            var mode = modes[index];
            if (!humanlike && mode != JobMode.Ingest)
                return false;
            s.job = job;
            s.driver = driver;

            switch (mode)
            {
                case JobMode.Ingest:
                    if (!(driver is JobDriver_Ingest ingest) || curToil(driver) != chewingToil(ingest))
                        return false;
                    s.subject = pawn.carryTracker?.CarriedThing ?? job.targetA.Thing;
                    if (!humanlike)
                    {
                        if (!settings.animals) return false;
                        s.kind = AnimKind.EatAnimal;
                        break;
                    }
                    s.cell = job.GetTarget(tableCellInd).Cell;
                    bool usesTable = s.subject?.def?.ingestible?.ingestHoldUsesTable ?? false;
                    s.kind = usesTable && s.cell.IsValid && s.cell.AdjacentToCardinal(pawn.Position) && s.cell.HasEatSurface(pawn.Map)
                        ? AnimKind.EatTable
                        : AnimKind.EatGround;
                    break;

                case JobMode.Bill:
                    var recipe = job.bill?.recipe;
                    if (recipe == null || pawn.carryTracker?.CarriedThing != null)
                        return false;
                    var giver = job.targetA.Thing;
                    if (giver == null)
                        return false;
                    if (giver.def.hasInteractionCell ? pawn.Position != giver.InteractionCell : !pawn.Position.AdjacentTo8WayOrInside(giver))
                        return false;
                    var rk = AnimClassifier.recipeKinds;
                    s.kind = recipe.index < rk.Length ? rk[recipe.index] : AnimKind.Hammer;
                    // Bill progress is resolved on the main thread by BillProgress.Of (props only).
                    break;

                case JobMode.Read:
                    if (driver is JobDriver_Reading reading && !reading.IsReading)
                        return false;
                    if (!(driver is JobDriver_Reading) && !driver.OnLastToil)
                        return false;
                    s.kind = AnimKind.Read;
                    s.subject = job.targetA.Thing;
                    break;

                case JobMode.BoardGame:
                case JobMode.Cards:
                case JobMode.Throw:
                    if (!driver.OnLastToil)
                        return false;
                    s.kind = mode == JobMode.BoardGame ? AnimKind.BoardGame : mode == JobMode.Cards ? AnimKind.Cards : AnimKind.Throw;
                    break;

                case JobMode.Wear:
                case JobMode.Unwear:
                {
                    int duration = mode == JobMode.Wear
                        ? (driver is JobDriver_Wear w ? wearDuration(w) : 0)
                        : (driver is JobDriver_RemoveApparel r ? unwearDuration(r) : 0);
                    int left = driver.ticksLeftThisToil;
                    if (duration <= 0 || left <= 0 || left > duration)
                        return false;
                    s.kind = mode == JobMode.Wear ? AnimKind.Wear : AnimKind.Unwear;
                    s.progress = 1f - (float)left / duration;
                    s.subject = job.targetA.Thing;
                    break;
                }

                case JobMode.Sweep:
                    s.kind = AnimKind.Sweep;
                    break;

                case JobMode.Ignore:
                    return false;

                default:
                    var skill = driver.ActiveSkill;
                    if (skill == null)
                        return false;
                    var sk = AnimClassifier.skillKinds;
                    s.kind = skill.index < sk.Length ? sk[skill.index] : AnimKind.None;
                    if (s.kind == AnimKind.Tend)
                        s.subject = job.targetB.Thing; // medicine used for tending, if any
                    break;
            }

            return s.kind != AnimKind.None && settings.IsOn(AnimClassifier.categoryOf[(int)s.kind]) && CanAnimate(pawn);
        }

        /// <summary>Checks left until a pawn is known to have an animatable job.</summary>
        private static bool CanAnimate(Pawn pawn)
        {
            if (pawn.GetPosture() != PawnPosture.Standing)
                return false;
            var renderer = pawn.Drawer?.renderer;
            return renderer != null && !renderer.HasAnimation;
        }
    }

    /// <summary>
    /// Remembers the tick a pawn equipped a weapon so a short flourish can play. Written on the
    /// main thread when equipment changes, read lock-free while drawing.
    /// </summary>
    public static class EquipFlourish
    {
        public const int Duration = 45;

        private struct Entry
        {
            public int tick;
            public Thing eq;
        }

        private static readonly ConcurrentDictionary<int, Entry> recent = new ConcurrentDictionary<int, Entry>();
        private static volatile int lastTick = int.MinValue / 2;
        private static volatile bool dirty;

        public static void Notify(Pawn pawn, Thing eq)
        {
            if (Current.ProgramState != ProgramState.Playing || pawn == null || !pawn.Spawned || !pawn.RaceProps.Humanlike)
                return;
            int now = GenTicks.TicksGame;
            recent[pawn.thingIDNumber] = new Entry { tick = now, eq = eq };
            lastTick = now;
            dirty = true;
        }

        public static bool TryGet(Pawn pawn, int now, out Thing eq, out float progress)
        {
            eq = null;
            progress = 0f;
            if (now - lastTick >= Duration || !recent.TryGetValue(pawn.thingIDNumber, out var e))
                return false;
            int age = now - e.tick;
            if (age < 0 || age >= Duration || e.eq == null || e.eq.Destroyed)
                return false;
            eq = e.eq;
            progress = (float)age / Duration;
            return true;
        }

        /// <summary>Drops stale entries once every flourish is over. Main thread only.</summary>
        public static void CleanupIfIdle()
        {
            if (dirty && GenTicks.TicksGame - lastTick >= Duration)
            {
                recent.Clear();
                dirty = false;
            }
        }
    }
}
