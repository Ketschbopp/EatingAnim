using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EatingAnim
{
    /// <summary>
    /// How far along a bill really is (0..1), so ingredients cook down and products take shape at
    /// the pace of the actual work: pawn skill, work speed, bench quality and stuff all count.
    ///
    /// The bill's total work (which depends on the stuff for things like weapons and apparel) is
    /// computed once per job and remembered; afterwards each frame is one table lookup and a
    /// division. Main thread only.
    /// </summary>
    public static class BillProgress
    {
        private class Total
        {
            public float value;
        }

        private static readonly ConditionalWeakTable<Job, Total> totals = new ConditionalWeakTable<Job, Total>();

        public static float Of(in AnimState s)
        {
            if (!(s.driver is JobDriver_DoBill bill) || s.job?.bill?.recipe == null)
                return -1f;
            if (!totals.TryGetValue(s.job, out var total))
            {
                total = new Total { value = ComputeTotal(s.job) };
                totals.Add(s.job, total);
            }
            // Never let a partly-done bill read as more than 100% if the estimate was low.
            if (bill.workLeft > total.value)
                total.value = bill.workLeft;
            return total.value > 0f ? Mathf.Clamp01(1f - bill.workLeft / total.value) : -1f;
        }

        private static float ComputeTotal(Job job)
        {
            var recipe = job.bill.recipe;
            try
            {
                Thing stuffSource = null;
                var placed = job.placedThings;
                if (placed != null)
                    foreach (var tc in placed)
                        if (tc?.thing != null && tc.thing.def.IsStuff) { stuffSource = tc.thing; break; }
                return recipe.WorkAmountTotal(stuffSource);
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[EatingAnim] Could not get work amount for {recipe.defName}: {e.Message}", recipe.shortHash ^ 0x2B11);
                return recipe.workAmount;
            }
        }
    }
}
