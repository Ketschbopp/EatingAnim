using HarmonyLib;
using RimWorld;
using Verse;

namespace EatingAnim
{
    // Optional "Disable shield belts": worn shield belts become inert. They block nothing, show no
    // bubble, no longer stop the wearer from shooting, and aren't favoured when pawns pick apparel.
    // Shields built into creatures (e.g. mechanoids) are left alone. Off by default.
    //
    // These run only when a pawn wearing a belt takes damage, draws or picks apparel, and are a
    // single bool check when the option is off.

    internal static class ShieldBelts
    {
        public static bool Disabled(CompShield shield) => EatingAnimMod.Settings.disableShieldBelts && shield.IsApparel;
    }

    [HarmonyPatch(typeof(CompShield), nameof(CompShield.PostPreApplyDamage))]
    public static class Patch_ShieldAbsorb
    {
        public static bool Prefix(CompShield __instance, ref bool absorbed)
        {
            if (!ShieldBelts.Disabled(__instance))
                return true;
            absorbed = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompShield), nameof(CompShield.CompDrawWornExtras))]
    public static class Patch_ShieldBubble
    {
        public static bool Prefix(CompShield __instance) => !ShieldBelts.Disabled(__instance);
    }

    [HarmonyPatch(typeof(CompShield), nameof(CompShield.CompAllowVerbCast))]
    public static class Patch_ShieldAllowShooting
    {
        public static bool Prefix(CompShield __instance, ref bool __result)
        {
            if (!ShieldBelts.Disabled(__instance))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompShield), nameof(CompShield.CompGetSpecialApparelScoreOffset))]
    public static class Patch_ShieldApparelScore
    {
        public static bool Prefix(CompShield __instance, ref float __result)
        {
            if (!ShieldBelts.Disabled(__instance))
                return true;
            __result = 0f;
            return false;
        }
    }
}
