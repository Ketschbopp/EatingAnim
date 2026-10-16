using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EatingAnim
{
    // All patches are postfixes on rendering methods: the game simulation (and TPS) is untouched.
    // Each one bails out after a couple of field reads for pawns that aren't animating.

    /// <summary>Whole-body offset (leaning, crouching, hopping). Runs on the parallel pre-draw path.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
    public static class Patch_GetBodyPos
    {
        public static void Postfix(Pawn ___pawn, PawnPosture posture, ref Vector3 __result)
        {
            if (posture != PawnPosture.Standing || !AnimStateUtility.TryGet(___pawn, out var s))
                return;
            var pose = PoseUtility.For(s);
            __result.x += pose.body.x;
            __result.z += pose.body.z;
        }
    }

    /// <summary>Whole-body tilt.</summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BodyAngle))]
    public static class Patch_BodyAngle
    {
        public static void Postfix(Pawn ___pawn, ref float __result)
        {
            if (!AnimStateUtility.TryGet(___pawn, out var s))
                return;
            __result += PoseUtility.For(s).bodyAngle;
        }
    }

    /// <summary>Head nods and chewing. Hair, headgear and face parts follow as children of the head node.</summary>
    [HarmonyPatch(typeof(PawnRenderNodeWorker_Head), nameof(PawnRenderNodeWorker_Head.OffsetFor))]
    public static class Patch_HeadOffset
    {
        public static void Postfix(PawnDrawParms parms, ref Vector3 __result)
        {
            // Portraits and the zoomed-out texture cache stay vanilla.
            if (parms.Portrait || parms.Cache || !AnimStateUtility.TryGet(parms.pawn, out var s))
                return;
            var head = PoseUtility.For(s).head;
            __result.x += head.x;
            __result.z += head.z;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker_Head), nameof(PawnRenderNodeWorker_Head.RotationFor))]
    public static class Patch_HeadRotation
    {
        public static void Postfix(PawnDrawParms parms, ref Quaternion __result)
        {
            if (parms.Portrait || parms.Cache || !AnimStateUtility.TryGet(parms.pawn, out var s))
                return;
            float angle = PoseUtility.For(s).headAngle;
            if (angle != 0f)
                __result *= Quaternion.AngleAxis(angle, Vector3.up);
        }
    }

    /// <summary>Hand-held props, drawn right after the pawn on the main thread.</summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_RenderPawnAt
    {
        public static void Postfix(Pawn ___pawn, Vector3 drawLoc)
        {
            EquipFlourish.CleanupIfIdle();
            if (!AnimStateUtility.TryGet(___pawn, out var s))
                return;
            var pose = PoseUtility.For(s);
            Props.DrawFor(___pawn, drawLoc, s, pose);
        }
    }

    /// <summary>
    /// Eating without a table: bring the real food item up to the mouth, in step with the head.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Ingest), nameof(JobDriver_Ingest.ModifyCarriedThingDrawPosWorker))]
    public static class Patch_IngestCarriedPos
    {
        public static void Postfix(ref Vector3 drawPos, Pawn pawn, ref bool __result)
        {
            if (!EatingAnimMod.Settings.props || !AnimStateUtility.TryGet(pawn, out var s) || s.kind != AnimKind.EatGround)
                return;
            var pose = PoseUtility.For(s);
            float bite = Curves.Bite(Curves.Cycle(s.t, 96f));
            Vector3 hold, mouth;
            switch (s.rot.AsInt)
            {
                case 0: hold = new Vector3(-0.1f, 0f, 0.02f); mouth = new Vector3(0f, 0f, 0.2f); break;
                case 1: hold = new Vector3(0.24f, 0f, -0.06f); mouth = new Vector3(0.18f, 0f, 0.2f); break;
                case 2: hold = new Vector3(0.1f, 0f, -0.1f); mouth = new Vector3(0.02f, 0f, 0.14f); break;
                default: hold = new Vector3(-0.24f, 0f, -0.06f); mouth = new Vector3(-0.18f, 0f, 0.2f); break;
            }
            Vector3 root = pawn.DrawPos + pose.body;
            Vector3 target = root + Vector3.Lerp(hold, mouth + pose.head, bite);
            if (!__result)
            {
                // Vanilla had no special hold offset and would fall back to the generic carry spot.
                __result = true;
            }
            drawPos.x = target.x;
            drawPos.z = target.z;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded))]
    public static class Patch_EquipmentAdded
    {
        public static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
        {
            if (EatingAnimMod.Settings.enabled && EatingAnimMod.Settings.equip)
                EquipFlourish.Notify(__instance.pawn, eq);
        }
    }
}
