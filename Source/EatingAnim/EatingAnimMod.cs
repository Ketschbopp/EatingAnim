using HarmonyLib;
using UnityEngine;
using Verse;

namespace EatingAnim
{
    public class EatingAnimMod : Mod
    {
        public static AnimSettings Settings;

        public EatingAnimMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AnimSettings>();
            Settings.Refresh();
            new Harmony("ketschbopp.eatinganim").PatchAll(typeof(EatingAnimMod).Assembly);
        }

        public override string SettingsCategory() => "EA_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var s = Settings;
            var list = new Listing_Standard();
            list.Begin(inRect);

            list.CheckboxLabeled("EA_Enabled".Translate(), ref s.enabled, "EA_Enabled_Desc".Translate());
            list.GapLine();

            list.Label("EA_Categories".Translate());
            list.CheckboxLabeled("EA_Eating".Translate(), ref s.eating, "EA_Eating_Desc".Translate());
            list.CheckboxLabeled("EA_Cooking".Translate(), ref s.cooking, "EA_Cooking_Desc".Translate());
            list.CheckboxLabeled("EA_Butchering".Translate(), ref s.butchering, "EA_Butchering_Desc".Translate());
            list.CheckboxLabeled("EA_Crafting".Translate(), ref s.crafting, "EA_Crafting_Desc".Translate());
            list.CheckboxLabeled("EA_Work".Translate(), ref s.work, "EA_Work_Desc".Translate());
            list.CheckboxLabeled("EA_Games".Translate(), ref s.games, "EA_Games_Desc".Translate());
            list.CheckboxLabeled("EA_Reading".Translate(), ref s.reading, "EA_Reading_Desc".Translate());
            list.CheckboxLabeled("EA_Apparel".Translate(), ref s.apparel, "EA_Apparel_Desc".Translate());
            list.CheckboxLabeled("EA_Equip".Translate(), ref s.equip, "EA_Equip_Desc".Translate());
            list.GapLine();

            list.CheckboxLabeled("EA_Animals".Translate(), ref s.animals, "EA_Animals_Desc".Translate());
            list.CheckboxLabeled("EA_Props".Translate(), ref s.props, "EA_Props_Desc".Translate());
            list.CheckboxLabeled("EA_HideZoomed".Translate(), ref s.hidePropsZoomedOut, "EA_HideZoomed_Desc".Translate());
            list.Gap();

            s.intensity = list.SliderLabeled("EA_Intensity".Translate(s.intensity.ToStringPercent()), s.intensity, 0.25f, 1.75f, 0.5f, "EA_Intensity_Desc".Translate());
            s.speed = list.SliderLabeled("EA_Speed".Translate(s.speed.ToStringPercent()), s.speed, 0.5f, 1.75f, 0.5f, "EA_Speed_Desc".Translate());
            list.Gap();

            list.GapLine();
            list.Label("EA_Other".Translate());
            list.CheckboxLabeled("EA_DisableShieldBelts".Translate(), ref s.disableShieldBelts, "EA_DisableShieldBelts_Desc".Translate());
            list.Gap();

            if (list.ButtonText("EA_Reset".Translate()))
                s.ResetToDefaults();

            list.End();
            s.Refresh();
        }
    }
}
