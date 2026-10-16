using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace EatingAnim
{
    public enum AnimKind : byte
    {
        None,
        EatTable,
        EatGround,
        EatAnimal,
        Cook,
        Mix,
        Butcher,
        Hammer,
        Sew,
        Chisel,
        Construct,
        Mine,
        Plant,
        Sweep,
        Write,
        Tend,
        Read,
        BoardGame,
        Cards,
        Throw,
        Wear,
        Unwear,
        Equip,
        Count
    }

    /// <summary>How a job is turned into an animation; resolved once per JobDef at startup.</summary>
    internal enum JobMode : byte
    {
        BySkill,   // anything else: use the active toil's skill (covers vanilla and modded work)
        Ingest,
        Bill,
        Read,
        BoardGame,
        Cards,
        Throw,
        Wear,
        Unwear,
        Sweep,
        Ignore
    }

    /// <summary>
    /// Lookup tables built once after defs load. Every runtime query is an array index, so the
    /// render path never touches dictionaries, strings or reflection.
    /// </summary>
    public static class AnimClassifier
    {
        internal static JobMode[] jobModes = Array.Empty<JobMode>();
        internal static AnimKind[] recipeKinds = Array.Empty<AnimKind>();
        internal static AnimKind[] skillKinds = Array.Empty<AnimKind>();
        internal static bool[] recipeSparks = Array.Empty<bool>();
        public static readonly AnimCategory[] categoryOf = new AnimCategory[(int)AnimKind.Count];
        public static bool Ready;

        static AnimClassifier()
        {
            categoryOf[(int)AnimKind.EatTable] = AnimCategory.Eating;
            categoryOf[(int)AnimKind.EatGround] = AnimCategory.Eating;
            categoryOf[(int)AnimKind.EatAnimal] = AnimCategory.Eating;
            categoryOf[(int)AnimKind.Cook] = AnimCategory.Cooking;
            categoryOf[(int)AnimKind.Mix] = AnimCategory.Crafting;
            categoryOf[(int)AnimKind.Butcher] = AnimCategory.Butchering;
            categoryOf[(int)AnimKind.Hammer] = AnimCategory.Crafting;
            categoryOf[(int)AnimKind.Sew] = AnimCategory.Crafting;
            categoryOf[(int)AnimKind.Chisel] = AnimCategory.Crafting;
            categoryOf[(int)AnimKind.Construct] = AnimCategory.Work;
            categoryOf[(int)AnimKind.Mine] = AnimCategory.Work;
            categoryOf[(int)AnimKind.Plant] = AnimCategory.Work;
            categoryOf[(int)AnimKind.Sweep] = AnimCategory.Work;
            categoryOf[(int)AnimKind.Write] = AnimCategory.Work;
            categoryOf[(int)AnimKind.Tend] = AnimCategory.Work;
            categoryOf[(int)AnimKind.Read] = AnimCategory.Reading;
            categoryOf[(int)AnimKind.BoardGame] = AnimCategory.Games;
            categoryOf[(int)AnimKind.Cards] = AnimCategory.Games;
            categoryOf[(int)AnimKind.Throw] = AnimCategory.Games;
            categoryOf[(int)AnimKind.Wear] = AnimCategory.Apparel;
            categoryOf[(int)AnimKind.Unwear] = AnimCategory.Apparel;
            categoryOf[(int)AnimKind.Equip] = AnimCategory.Equip;
        }

        public static void Build()
        {
            var jobs = DefDatabase<JobDef>.AllDefsListForReading;
            var modes = new JobMode[MaxIndex(jobs) + 1];
            var cerebral = DefDatabase<JoyKindDef>.GetNamedSilentFail("Gaming_Cerebral");
            var dexterity = DefDatabase<JoyKindDef>.GetNamedSilentFail("Gaming_Dexterity");
            foreach (var def in jobs)
                modes[def.index] = ModeFor(def, cerebral, dexterity);
            jobModes = modes;

            var recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            var rk = new AnimKind[MaxIndex(recipes) + 1];
            foreach (var def in recipes)
                rk[def.index] = KindForRecipe(def);
            recipeKinds = rk;
            var sparks = new bool[rk.Length];
            foreach (var def in recipes)
                sparks[def.index] = EffectIs(def, "Smith") || EffectIs(def, "Smelt");
            recipeSparks = sparks;

            var skills = DefDatabase<SkillDef>.AllDefsListForReading;
            var sk = new AnimKind[MaxIndex(skills) + 1];
            foreach (var def in skills)
                sk[def.index] = KindForSkill(def);
            skillKinds = sk;

            Ready = true;
        }

        private static int MaxIndex<T>(List<T> defs) where T : Def
        {
            int max = 0;
            foreach (var d in defs)
                if (d.index > max) max = d.index;
            return max;
        }

        private static bool Is<T>(Type driver) => driver != null && typeof(T).IsAssignableFrom(driver);

        private static JobMode ModeFor(JobDef def, JoyKindDef cerebral, JoyKindDef dexterity)
        {
            var driver = def.driverClass;
            if (Is<JobDriver_Ingest>(driver)) return JobMode.Ingest;
            if (Is<JobDriver_DoBill>(driver)) return JobMode.Bill;
            if (Is<JobDriver_Reading>(driver)) return JobMode.Read;
            if (Is<JobDriver_Wear>(driver)) return JobMode.Wear;
            if (Is<JobDriver_RemoveApparel>(driver)) return JobMode.Unwear;
            if (Is<JobDriver_PlayPoker>(driver)) return JobMode.Cards;
            if (def == JobDefOf.Clean || def == JobDefOf.ClearSnow) return JobMode.Sweep;
            if (def.joyKind != null)
            {
                // Modded books, newspapers and documents usually reuse the Reading joy kind.
                if (def.joyKind == JoyKindDefOf.Reading) return JobMode.Read;
                if (def.joyKind == cerebral) return JobMode.BoardGame;
                if (def.joyKind == dexterity) return JobMode.Throw;
            }
            return JobMode.BySkill;
        }

        private static bool EffectIs(RecipeDef r, string part)
        {
            var name = r.effectWorking?.defName;
            return name != null && name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AnimKind KindForRecipe(RecipeDef r)
        {
            try
            {
                if (r.IsSurgery) return AnimKind.None; // surgery stays vanilla
                if ((r.specialProducts != null && r.specialProducts.Contains(SpecialProductType.Butchery)) || EffectIs(r, "Butcher"))
                    return AnimKind.Butcher;
                if (r.workSkill == SkillDefOf.Cooking || EffectIs(r, "Cook")) return AnimKind.Cook;
                if (EffectIs(r, "Tailor")) return AnimKind.Sew;
                if (EffectIs(r, "Sculpt") || r.workSkill == SkillDefOf.Artistic) return AnimKind.Chisel;
                if (EffectIs(r, "Drug") || r.workSkill == SkillDefOf.Medicine) return AnimKind.Mix;
                if (r.workSkill == SkillDefOf.Intellectual) return AnimKind.Write;
            }
            catch (Exception e)
            {
                Log.WarningOnce($"[EatingAnim] Could not classify recipe {r.defName}: {e.Message}", r.shortHash ^ 0x5EA7);
            }
            return AnimKind.Hammer;
        }

        private static AnimKind KindForSkill(SkillDef s)
        {
            if (s == SkillDefOf.Construction) return AnimKind.Construct;
            if (s == SkillDefOf.Mining) return AnimKind.Mine;
            if (s == SkillDefOf.Plants) return AnimKind.Plant;
            if (s == SkillDefOf.Cooking) return AnimKind.Cook;
            if (s == SkillDefOf.Crafting) return AnimKind.Hammer;
            if (s == SkillDefOf.Artistic) return AnimKind.Chisel;
            if (s == SkillDefOf.Intellectual) return AnimKind.Write;
            if (s == SkillDefOf.Medicine) return AnimKind.None; // tending stays vanilla
            if (s == SkillDefOf.Animals) return AnimKind.Tend;
            // Shooting, Melee, Social: never override combat or conversations.
            return AnimKind.None;
        }
    }

    [StaticConstructorOnStartup]
    internal static class AnimStartup
    {
        static AnimStartup()
        {
            AnimClassifier.Build();
            Props.Init();
        }
    }
}
