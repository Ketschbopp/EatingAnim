using Verse;

namespace EatingAnim
{
    /// <summary>Groups of animations that can be toggled together in the settings window.</summary>
    public enum AnimCategory : byte
    {
        Eating,
        Cooking,
        Butchering,
        Crafting,
        Work,
        Games,
        Reading,
        Apparel,
        Equip,
        Count
    }

    public class AnimSettings : ModSettings
    {
        public bool enabled = true;
        public bool eating = true;
        public bool cooking = true;
        public bool butchering = true;
        public bool crafting = true;
        public bool work = true;
        public bool games = true;
        public bool reading = true;
        public bool apparel = true;
        public bool equip = true;

        public bool animals = true;
        public bool props = true;
        public bool hidePropsZoomedOut = true;
        public float intensity = 1f;
        public float speed = 1f;

        // Flattened copy of the category toggles so the render path is a single array read.
        private readonly bool[] categoryOn = new bool[(int)AnimCategory.Count];

        public bool IsOn(AnimCategory c) => categoryOn[(int)c];

        public void Refresh()
        {
            categoryOn[(int)AnimCategory.Eating] = eating;
            categoryOn[(int)AnimCategory.Cooking] = cooking;
            categoryOn[(int)AnimCategory.Butchering] = butchering;
            categoryOn[(int)AnimCategory.Crafting] = crafting;
            categoryOn[(int)AnimCategory.Work] = work;
            categoryOn[(int)AnimCategory.Games] = games;
            categoryOn[(int)AnimCategory.Reading] = reading;
            categoryOn[(int)AnimCategory.Apparel] = apparel;
            categoryOn[(int)AnimCategory.Equip] = equip;
        }

        public void ResetToDefaults()
        {
            enabled = eating = cooking = butchering = crafting = work = games = reading = apparel = equip = true;
            animals = props = hidePropsZoomedOut = true;
            intensity = 1f;
            speed = 1f;
            Refresh();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref eating, "eating", true);
            Scribe_Values.Look(ref cooking, "cooking", true);
            Scribe_Values.Look(ref butchering, "butchering", true);
            Scribe_Values.Look(ref crafting, "crafting", true);
            Scribe_Values.Look(ref work, "work", true);
            Scribe_Values.Look(ref games, "games", true);
            Scribe_Values.Look(ref reading, "reading", true);
            Scribe_Values.Look(ref apparel, "apparel", true);
            Scribe_Values.Look(ref equip, "equip", true);
            Scribe_Values.Look(ref animals, "animals", true);
            Scribe_Values.Look(ref props, "props", true);
            Scribe_Values.Look(ref hidePropsZoomedOut, "hidePropsZoomedOut", true);
            Scribe_Values.Look(ref intensity, "intensity", 1f);
            Scribe_Values.Look(ref speed, "speed", 1f);
            Refresh();
        }
    }
}
