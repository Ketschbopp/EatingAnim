using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EatingAnim
{
    /// <summary>
    /// Draws the hand-held side of each animation. Real in-game things (the food being eaten, the
    /// bill's ingredients and product, the book being read, the medicine, the apparel, the weapon,
    /// the corpse's meat) are drawn with their own graphics; only tools the game has no item for
    /// (pans, cleavers, quills...) use this mod's textures.
    ///
    /// Main thread only. Called solely for pawns the game is already drawing, so off-screen pawns
    /// cost nothing.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Props
    {
        private static Material FryingPan, Pot, Spatula, WoodenSpoon, Spoon, Fork, Cleaver, Knife,
            Hammer, Mallet, Chisel, Needle, Saw, Pickaxe, Trowel, Sickle, Broom, Quill, Paper, Page, Bandage,
            Card, CardFan, GamePiece, Horseshoe, Spark, Steam, Dust,
            Beaker, BeakerLiquid, Flask, FlaskLiquid, Erlenmeyer, ErlenmeyerLiquid, TubeRack, Drop, Bubble;

        private static MaterialPropertyBlock fadeBlock;
        private static float cacheZoomCutoff = 18f;
        private static bool initialized;

        private static readonly Func<Book, Graphic> bookOpenGraphic = CreateBookGraphicGetter();

        private static Func<Book, Graphic> CreateBookGraphicGetter()
        {
            var getter = AccessTools.PropertyGetter(typeof(Book), "OpenGraphic");
            return getter == null ? null : AccessTools.MethodDelegate<Func<Book, Graphic>>(getter);
        }

        public static void Init()
        {
            Material Cut(string name) => MaterialPool.MatFrom("EatingAnim/" + name, ShaderDatabase.Cutout);
            Material Fade(string name) => MaterialPool.MatFrom("EatingAnim/" + name, ShaderDatabase.Transparent);

            FryingPan = Cut("FryingPan"); Pot = Cut("Pot"); Spatula = Cut("Spatula"); WoodenSpoon = Cut("WoodenSpoon");
            Spoon = Cut("Spoon"); Fork = Cut("Fork"); Cleaver = Cut("Cleaver"); Knife = Cut("Knife");
            Hammer = Cut("Hammer"); Mallet = Cut("Mallet");
            Chisel = Cut("Chisel"); Needle = Cut("Needle"); Saw = Cut("Saw"); Pickaxe = Cut("Pickaxe");
            Trowel = Cut("Trowel"); Sickle = Cut("Sickle"); Broom = Cut("Broom"); Quill = Cut("Quill");
            Paper = Cut("Paper"); Page = Cut("Page"); Bandage = Cut("Bandage"); Card = Cut("Card");
            CardFan = Cut("CardFan"); GamePiece = Cut("GamePiece"); Horseshoe = Cut("Horseshoe");
            Spark = Fade("Spark"); Steam = Fade("Steam"); Dust = Fade("Dust");
            Beaker = Fade("Beaker"); BeakerLiquid = Fade("BeakerLiquid"); Flask = Fade("Flask");
            FlaskLiquid = Fade("FlaskLiquid"); Erlenmeyer = Fade("Erlenmeyer"); ErlenmeyerLiquid = Fade("ErlenmeyerLiquid");
            TubeRack = Fade("TubeRack"); Drop = Fade("Drop"); Bubble = Fade("Bubble");
            fadeBlock = new MaterialPropertyBlock();

            try
            {
                var f = AccessTools.Field(typeof(PawnRenderer), "CachedPawnTextureMinCameraZoom");
                if (f != null) cacheZoomCutoff = (float)f.GetValue(null);
            }
            catch (Exception e)
            {
                Log.Warning("[EatingAnim] Using default zoom cutoff: " + e.Message);
            }
            initialized = true;
        }

        // ------------------------------------------------------------------ facing frame

        /// <summary>Converts "reach forward / raise / to the right hand" into screen space for any facing.</summary>
        private struct Frame
        {
            public Vector3 origin;
            public Vector3 fwd;
            public float side;   // +1 east, -1 west, 0 north/south
            public int dir;      // 0 N, 1 E, 2 S, 3 W

            public bool Horizontal => side != 0f;
            public bool North => dir == 0;
            public bool Flip => side < 0f;

            public Vector3 At(float reach, float height, float lateral = 0f, float lift = 0f)
            {
                Vector3 lat;
                if (dir == 2) lat = new Vector3(-lateral, 0f, 0f);         // pawn's right is screen left
                else if (dir == 0) lat = new Vector3(lateral, 0f, 0f);     // seen from behind
                else lat = new Vector3(0f, 0f, -lateral * 0.35f);          // near hand slightly lower
                return origin + fwd * reach + new Vector3(0f, lift, height) + lat;
            }

            /// <summary>
            /// Tool rotation. sideAngle is used for east (mirrored for west); frontAngle for south
            /// (mirrored for north, where the pawn's right hand is on the other side of the screen).
            /// </summary>
            public float Angle(float sideAngle, float frontAngle) =>
                Horizontal ? sideAngle * side : dir == 2 ? frontAngle : -frontAngle;

            /// <summary>Where the mouth is, for things brought up to eat.</summary>
            public Vector3 Mouth(Vector3 headOffset)
            {
                var p = origin + headOffset;
                switch (dir)
                {
                    case 0: return p + new Vector3(0f, 0f, 0.24f);
                    case 2: return p + new Vector3(0f, -0.0f, 0.2f);
                    default: return p + new Vector3(0.13f * side, 0f, 0.22f);
                }
            }
        }

        private const float Step = 0.0004f; // altitude step between stacked props

        // ------------------------------------------------------------------ drawing helpers

        private static void Draw(Material mat, Vector3 pos, float angle, float size, bool flip = false)
        {
            if (mat == null) return;
            var m = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(flip ? MeshPool.plane10Flip : MeshPool.plane10, m, mat, 0);
        }

        private static void DrawFaded(Material mat, Vector3 pos, float angle, float size, float alpha)
        {
            if (mat == null || alpha <= 0.01f) return;
            fadeBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            var m = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, m, mat, 0, null, 0, fadeBlock);
        }

        private static void DrawTinted(Material mat, Vector3 pos, float angle, float size, Color color, bool flip = false)
        {
            if (mat == null || color.a <= 0.01f) return;
            fadeBlock.SetColor(ShaderPropertyIDs.Color, color);
            var m = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(flip ? MeshPool.plane10Flip : MeshPool.plane10, m, mat, 0, null, 0, fadeBlock);
        }

        /// <summary>Point along a tool from its centre, for things carried at its tip.</summary>
        private static Vector3 Tip(Vector3 center, float angle, float dist)
        {
            float r = angle * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Sin(r) * dist, 0f, Mathf.Cos(r) * dist);
        }

        private static Material MatOf(Thing t)
        {
            if (t == null || t.Destroyed) return null;
            try { return t.Graphic?.MatSingle; }
            catch { return null; }
        }

        private static Material MatOf(ThingDef def)
        {
            if (def?.graphicData == null) return null;
            try { return def.graphic?.MatSingle; }
            catch { return null; }
        }

        private static Thing Ingredient(in AnimState s, int i)
        {
            var placed = s.job?.placedThings;
            if (placed == null || placed.Count == 0) return null;
            for (int n = 0; n < placed.Count; n++)
            {
                var t = placed[(i + n) % placed.Count]?.thing;
                if (t != null && !(t is Corpse) && !t.Destroyed) return t;
            }
            return null;
        }

        private static int IngredientCount(in AnimState s) => s.job?.placedThings?.Count ?? 0;

        private static Corpse FindCorpse(in AnimState s)
        {
            if (s.job == null) return null;
            if (s.job.placedThings != null)
                foreach (var tc in s.job.placedThings)
                    if (tc?.thing is Corpse c) return c;
            return s.job.targetB.Thing as Corpse;
        }

        // ------------------------------------------------------------------ entry point

        public static void DrawFor(Pawn pawn, Vector3 drawLoc, in AnimState s, in AnimPose pose)
        {
            if (!initialized) return;
            var settings = EatingAnimMod.Settings;
            if (!settings.props) return;
            if (settings.hidePropsZoomedOut && Find.CameraDriver.ZoomRootSize > cacheZoomCutoff) return;

            var f = new Frame
            {
                fwd = Curves.Forward(s.rot),
                side = Curves.Side(s.rot),
                dir = s.rot.AsInt
            };
            f.origin = drawLoc + pose.body;
            f.origin.y = drawLoc.y + PawnRenderUtility.AltitudeForLayer(f.North ? PawnRenderUtility.Layer_Carried_Behind : PawnRenderUtility.Layer_Carried);

            float t = s.t;
            switch (s.kind)
            {
                case AnimKind.EatTable: EatTable(pawn, f, s, pose); break;
                case AnimKind.Cook: Cook(f, s, t); break;
                case AnimKind.Chem: Chem(f, s, t); break;
                case AnimKind.Butcher: Butcher(f, s, t); break;
                case AnimKind.Hammer: Craft(f, s, t); break;
                case AnimKind.Sew: Sew(f, s, t); break;
                case AnimKind.Chisel: ChiselWork(f, s, t); break;
                case AnimKind.Construct: Construct(f, s, t); break;
                case AnimKind.Mine: Mine(f, t); break;
                case AnimKind.Plant: Plant(f, s, t); break;
                case AnimKind.Sweep: Sweep(f, t); break;
                case AnimKind.Write: Write(f, t); break;
                case AnimKind.Tend: Tend(f, s, t); break;
                case AnimKind.Read: Read(pawn, f, s, t); break;
                case AnimKind.BoardGame: BoardGame(f, t); break;
                case AnimKind.Cards: Cards(f, t); break;
                case AnimKind.Throw: Throw(f, s, t); break;
                case AnimKind.Wear: Apparel(f, s, t, true); break;
                case AnimKind.Unwear: Apparel(f, s, t, false); break;
                case AnimKind.Equip: Equip(pawn, f, s); break;
            }
        }

        // ------------------------------------------------------------------ eating

        private static void EatTable(Pawn pawn, Frame f, in AnimState s, in AnimPose pose)
        {
            float p = Curves.Cycle(s.t, 84f);
            float bite = Curves.Bite(p);
            Vector3 table = s.cell.ToVector3Shifted();
            table.y = f.origin.y;
            Vector3 dip = Vector3.Lerp(f.At(0.3f, -0.02f, 0.08f), table, 0.55f);
            Vector3 mouth = f.Mouth(pose.head);
            mouth.y = f.origin.y;
            Vector3 pos = Vector3.Lerp(dip, mouth, bite) + new Vector3(0f, 0f, 0.06f * Curves.Hump(bite, 0f, 1f));
            float angle = f.Angle(Mathf.Lerp(125f, -25f, bite), Mathf.Lerp(170f, 15f, bite));
            bool fork = (pawn.thingIDNumber & 1) == 1;
            Draw(fork ? Fork : Spoon, pos, angle, 0.4f, f.Flip);

            // A morsel of the actual food rides on the utensil on the way up.
            if (bite > 0.05f && p < 0.4f)
            {
                var food = MatOf(s.subject);
                Draw(food, Tip(pos, angle, 0.14f) + new Vector3(0f, Step, 0f), 0f, 0.17f);
            }
        }

        // ------------------------------------------------------------------ cooking

        private static void Cook(Frame f, in AnimState s, float t)
        {
            var product = s.job?.bill?.recipe?.ProducedThingDef;
            bool pan = product?.ingestible != null && product.ingestible.IsMeal;
            float progress = BillProgress.Of(s);
            if (progress < 0f) progress = Curves.Cycle(t, 900f);

            // Pan tosses now and then; the pot just bubbles.
            float toss = pan ? Curves.Hump(Curves.Cycle(t, 95f), 0.0f, 0.22f) : 0f;
            Vector3 vessel = f.At(0.55f, -0.04f) + f.fwd * (0.03f * toss) + new Vector3(0f, 0f, 0.025f * toss);
            float vesselAngle = pan ? f.Angle(90f, 180f) : 0f;
            if (pan && f.North) vesselAngle = 0f;
            Draw(pan ? FryingPan : Pot, vessel, vesselAngle, 0.6f, f.Flip);
            Vector3 inside = vessel + (pan ? Vector3.zero : new Vector3(0f, 0f, 0.1f));

            // Contents: the real ingredients cook down while the real product (e.g. a lavish meal) forms.
            float ingredientScale = 1f - Curves.Smooth((progress - 0.35f) / 0.5f);
            int count = Mathf.Min(IngredientCount(s), 3);
            for (int i = 0; i < count; i++)
            {
                var mat = MatOf(Ingredient(s, i));
                float a = t * 0.05f + i * 2.1f;
                Vector3 jitter = new Vector3(Mathf.Cos(a) * 0.06f, Step * (2 + i), Mathf.Sin(a) * 0.03f + 0.03f * toss * (i + 1));
                Draw(mat, inside + jitter, a * 20f, 0.2f * ingredientScale);
            }
            float productScale = Curves.Smooth((progress - 0.45f) / 0.5f);
            if (productScale > 0.02f)
                Draw(MatOf(product), inside + new Vector3(0f, Step * 6, 0.02f * toss), 0f, 0.42f * productScale);

            // Stirring with a spatula (pan) or wooden spoon (pot).
            float stir = Curves.Cycle(t, 40f) * Curves.TwoPi;
            Vector3 tool = inside + new Vector3(Mathf.Cos(stir) * 0.07f, Step * 8, 0.1f + Mathf.Sin(stir) * 0.035f);
            Draw(pan ? Spatula : WoodenSpoon, tool, f.Angle(-30f, 20f) + Mathf.Sin(stir) * 12f, 0.42f, f.Flip);

            TossIngredient(f, s, t, inside, 150f);
            SteamPuffs(inside, t, 1f);
        }

        // Reagent colours the flask cycles through, and the beaker's colour as a batch matures.
        private static readonly Color[] reagents =
        {
            new Color(0.72f, 0.42f, 0.95f), new Color(0.98f, 0.62f, 0.22f),
            new Color(0.36f, 0.82f, 0.96f), new Color(0.98f, 0.38f, 0.52f)
        };
        private static readonly Color brewStart = new Color(0.55f, 0.78f, 0.32f);
        private static readonly Color brewEnd = new Color(0.62f, 0.38f, 0.92f);

        /// <summary>A spot on the bench: spread runs along the bench, whichever way the pawn faces.</summary>
        private static Vector3 Bench(Frame f, float reach, float spread, float lift)
        {
            Vector3 along = f.Horizontal ? new Vector3(0f, 0f, spread * 1.1f) : new Vector3(f.dir == 2 ? -spread : spread, 0f, 0f);
            return f.origin + f.fwd * reach + along + new Vector3(0f, lift, -0.04f);
        }

        /// <summary>
        /// Drug lab: take a flask from the test-tube rack, pour a coloured stream into the beaker,
        /// put it back, swirl the beaker and drop in a real ingredient, while the batch fizzes and
        /// changes colour and the real product forms beside it.
        /// </summary>
        private static void Chem(Frame f, in AnimState s, float t)
        {
            const float cycle = PoseUtility.ChemCycle;
            float p = Curves.Cycle(t, cycle);
            int round = (int)(t / cycle);
            float progress = BillProgress.Of(s);
            if (progress < 0f) progress = Curves.Cycle(t, 1200f);
            Color reagent = reagents[round % reagents.Length];

            // Bench layout: rack on the free-hand side, beaker in the middle, product and a
            // bubbling Erlenmeyer flask on the other side.
            Vector3 rack = Bench(f, 0.62f, -0.3f, Step);
            Vector3 product = Bench(f, 0.55f, 0.24f, Step * 2);
            Vector3 erlen = Bench(f, 0.68f, 0.3f, Step);

            float pour = Curves.Smooth((p - 0.18f) / 0.1f) * (1f - Curves.Smooth((p - 0.48f) / 0.07f));
            float swirlEnv = Curves.Hump(p, 0.66f, 0.96f);
            Vector3 beaker = Bench(f, 0.6f, 0f, Step * 3)
                + new Vector3(Mathf.Cos(t * 0.5f) * 0.025f * swirlEnv, 0f, 0.05f * swirlEnv + Mathf.Sin(t * 0.5f) * 0.012f * swirlEnv);
            float beakerAngle = Mathf.Sin(t * 0.5f) * 10f * swirlEnv;

            Draw(TubeRack, rack, 0f, 0.34f);

            DrawTinted(ErlenmeyerLiquid, erlen, 0f, 0.3f, Color.Lerp(brewEnd, reagent, 0.4f));
            DrawTinted(Erlenmeyer, erlen + new Vector3(0f, Step, 0f), 0f, 0.3f, Color.white);
            Bubbles(erlen + new Vector3(0f, Step * 2, -0.04f), t + 37f, 0.07f, 0.5f, Color.white);

            float productScale = Curves.Smooth((progress - 0.3f) / 0.65f);
            if (productScale > 0.02f)
                Draw(MatOf(s.job?.bill?.recipe?.ProducedThingDef), product, 0f, 0.3f * productScale);

            // Beaker: tinted liquid under clear glass; the pour pulls the colour toward the reagent.
            Color brew = Color.Lerp(Color.Lerp(brewStart, brewEnd, progress), reagent, 0.45f * pour);
            DrawTinted(BeakerLiquid, beaker, beakerAngle, 0.36f, brew);
            DrawTinted(Beaker, beaker + new Vector3(0f, Step, 0f), beakerAngle, 0.36f, Color.white);
            Bubbles(beaker + new Vector3(0f, Step * 2, -0.06f), t, 0.09f, 0.6f + 0.8f * pour + 0.4f * swirlEnv, Color.Lerp(brew, Color.white, 0.5f));

            // Flask: rack -> above the beaker -> tilt and pour -> back to the rack.
            float go = Curves.Smooth(p / 0.16f) * (1f - Curves.Smooth((p - 0.56f) / 0.12f));
            Vector3 hold = f.Horizontal
                ? Bench(f, 0.48f, 0f, Step * 6) + new Vector3(0f, 0f, 0.24f)
                : Bench(f, 0.6f, -0.15f, Step * 6) + new Vector3(0f, 0f, 0.22f);
            Vector3 flask = Vector3.Lerp(rack + new Vector3(0f, Step * 5, 0.04f), hold, go)
                + new Vector3(0f, 0f, 0.08f * Curves.Hump(go, 0f, 1f));
            // Facing south the flask is held on the screen-right of the beaker, so it tips
            // counter-clockwise; Frame.Angle mirrors that for north.
            float flaskAngle = f.Angle(115f * pour, -105f * pour);
            float fill = 1f - 0.65f * Curves.Smooth((p - 0.24f) / 0.26f);
            if (p > 0.68f) fill = 1f;
            DrawTinted(FlaskLiquid, flask, flaskAngle, 0.3f, new Color(reagent.r, reagent.g, reagent.b, fill));
            DrawTinted(Flask, flask + new Vector3(0f, Step, 0f), flaskAngle, 0.3f, Color.white);

            // The poured stream: drops falling from the flask's lip into the beaker.
            if (pour > 0.85f && p > 0.24f && p < 0.5f)
            {
                Vector3 lip = Tip(flask, flaskAngle, 0.14f);
                Vector3 surface = beaker + new Vector3(0f, 0f, 0.02f);
                for (int i = 0; i < 5; i++)
                {
                    float q = Curves.Cycle(t * 0.09f + i / 5f, 1f);
                    Vector3 pos = Vector3.Lerp(lip, surface, q * q) + new Vector3(0f, Step * 4, 0f);
                    DrawTinted(Drop, pos, 0f, 0.07f, reagent);
                }
            }

            // Then a real ingredient goes in while swirling (shifted so it lands in the swirl phase).
            TossIngredient(f, s, t + cycle * 0.3f, beaker + new Vector3(0f, 0f, 0.04f), cycle);
        }

        /// <summary>A few bubbles rising and popping inside a vessel.</summary>
        private static void Bubbles(Vector3 bottom, float t, float height, float rate, Color color)
        {
            for (int i = 0; i < 3; i++)
            {
                float q = Curves.Cycle(t * 0.02f * rate + i * 0.37f, 1f);
                Vector3 pos = bottom + new Vector3(Mathf.Sin(i * 2.3f + q * 6f) * 0.03f, 0f, height * q);
                DrawTinted(Bubble, pos, 0f, 0.035f + 0.02f * q, new Color(color.r, color.g, color.b, 0.9f * Curves.Hump(q, 0f, 1f)));
            }
        }

        /// <summary>A real ingredient arcs from the free hand into the vessel every so often.</summary>
        private static void TossIngredient(Frame f, in AnimState s, float t, Vector3 target, float period)
        {
            float p = Curves.Cycle(t, period);
            if (p > 0.22f) return;
            int n = IngredientCount(s);
            if (n == 0) return;
            var mat = MatOf(Ingredient(s, (int)(t / period) % n));
            float q = p / 0.22f;
            Vector3 from = f.At(0.15f, 0.12f, -0.22f);
            Vector3 pos = Vector3.Lerp(from, target, q) + new Vector3(0f, Step * 10, 0.28f * 4f * q * (1f - q));
            Draw(mat, pos, q * 300f, 0.22f * (1f - 0.35f * q));
        }

        private static void SteamPuffs(Vector3 at, float t, float strength)
        {
            for (int i = 0; i < 2; i++)
            {
                float p = Curves.Cycle(t + i * 45f, 90f);
                Vector3 pos = at + new Vector3(Mathf.Sin(p * 5f + i) * 0.05f, Step * 12, 0.08f + 0.4f * p);
                DrawFaded(Steam, pos, 0f, (0.18f + 0.3f * p) * strength, 0.55f * Curves.Hump(p, 0f, 1f));
            }
        }

        // ------------------------------------------------------------------ butchering / crafting

        private static void Strike(Frame f, Material tool, float t, float period, float strikeAt, float reach, float size,
            out float impact, out Vector3 hit)
        {
            Curves.Strike(Curves.Cycle(t, period), strikeAt, out float raise, out impact);
            float shake = Mathf.Sin(t * 2.7f) * 0.012f * impact;
            Vector3 pos = f.At(reach, 0.04f + 0.2f * raise, 0.06f) + new Vector3(shake, Step * 6, 0f);
            float angle = f.Angle(Mathf.Lerp(100f, -25f, raise), Mathf.Lerp(65f, -40f, raise));
            Draw(tool, pos, angle, size, f.Flip);
            hit = Tip(pos, angle, size * 0.35f);
        }

        private static void Butcher(Frame f, in AnimState s, float t)
        {
            Strike(f, Cleaver, t, 54f, 0.6f, 0.36f, 0.48f, out float impact, out Vector3 hit);
            if (impact <= 0f) return;
            // Chunks of the carcass's own meat fly off the block.
            var meat = MatOf(FindCorpse(s)?.InnerPawn?.RaceProps?.meatDef);
            float q = 1f - impact;
            if (Curves.Cycle(t, 54f) < 0.6f) q = 0f;
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 pos = hit + new Vector3(i * 0.16f * q, Step * 8, 0.18f * 4f * q * (1f - q));
                Draw(meat, pos, q * 200f * i, 0.16f);
            }
        }

        private static void Product(Frame f, in AnimState s, float baseScale, float reach = 0.42f)
        {
            var product = s.job?.bill?.recipe?.ProducedThingDef;
            float progress = BillProgress.Of(s);
            if (progress < 0f) progress = 0.6f;
            Draw(MatOf(product), f.At(reach, -0.06f, 0f, Step), 0f, baseScale * (0.45f + 0.55f * Curves.Smooth(progress)));
        }

        private static void Craft(Frame f, in AnimState s, float t)
        {
            Product(f, s, 0.42f);
            Strike(f, Hammer, t, 34f, 0.55f, 0.34f, 0.42f, out float impact, out Vector3 hit);
            var recipe = s.job?.bill?.recipe;
            var sparks = AnimClassifier.recipeSparks;
            if (impact > 0.05f && recipe != null && recipe.index < sparks.Length && sparks[recipe.index])
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.1f + t;
                    Vector3 pos = hit + new Vector3(Mathf.Cos(a) * 0.15f * (1f - impact), Step * 10, Mathf.Abs(Mathf.Sin(a)) * 0.15f * (1f - impact));
                    DrawFaded(Spark, pos, a * 40f, 0.14f, impact);
                }
            }
        }

        private static void Sew(Frame f, in AnimState s, float t)
        {
            Product(f, s, 0.45f, 0.38f);
            float p = Curves.Cycle(t, 30f);
            float pull = Curves.Hump(p, 0.4f, 0.9f);
            Vector3 pos = f.At(0.36f, 0.02f + 0.12f * pull, 0.04f + 0.08f * pull, Step * 6);
            Draw(Needle, pos, f.Angle(35f + 30f * pull, -25f - 30f * pull), 0.34f, f.Flip);
        }

        private static void ChiselWork(Frame f, in AnimState s, float t)
        {
            Product(f, s, 0.5f, 0.45f);
            Curves.Strike(Curves.Cycle(t, 22f), 0.55f, out float raise, out float impact);
            Vector3 chisel = f.At(0.32f, 0.04f, 0f, Step * 5) + f.fwd * (0.015f * impact);
            float ca = f.Angle(70f, 150f);
            Draw(Chisel, chisel, ca, 0.32f, f.Flip);
            Vector3 mallet = Tip(chisel, ca, -0.2f - 0.12f * raise) + new Vector3(0f, Step, 0f);
            Draw(Mallet, mallet, f.Angle(-20f - 30f * raise, 60f + 30f * raise), 0.3f, f.Flip);
            if (impact > 0.1f)
                DrawFaded(Dust, Tip(chisel, ca, 0.16f) + new Vector3(0f, Step * 2, 0.04f), t * 7f, 0.22f * impact, 0.5f * impact);
        }

        private static void Construct(Frame f, in AnimState s, float t)
        {
            var def = s.job?.def;
            if (def == JobDefOf.Deconstruct || def == JobDefOf.Uninstall)
            {
                float saw = Mathf.Sin(Curves.Cycle(t, 26f) * Curves.TwoPi);
                Vector3 pos = f.At(0.4f + 0.07f * saw, 0.05f, 0.04f, Step * 6);
                Draw(Saw, pos, f.Angle(110f, 160f), 0.46f, f.Flip);
                return;
            }
            Strike(f, Hammer, t, 40f, 0.55f, 0.4f, 0.44f, out float impact, out Vector3 hit);
            if (impact > 0.1f)
                DrawFaded(Dust, hit + new Vector3(0f, Step * 4, 0.05f), t * 5f, 0.3f * impact, 0.45f * impact);
        }

        private static void Mine(Frame f, float t)
        {
            Curves.Strike(Curves.Cycle(t, 64f), 0.58f, out float raise, out float impact);
            Vector3 pos = f.At(0.42f, 0.06f + 0.24f * raise, 0.04f, Step * 6);
            float angle = f.Angle(Mathf.Lerp(105f, -45f, raise), Mathf.Lerp(150f, -30f, raise));
            Draw(Pickaxe, pos, angle, 0.62f, f.Flip);
            if (impact > 0.05f)
            {
                Vector3 hit = Tip(pos, angle, 0.24f);
                DrawFaded(Dust, hit + new Vector3(0f, Step * 4, 0.06f * (1f - impact)), t * 4f, 0.25f + 0.25f * (1f - impact), 0.6f * impact);
            }
        }

        private static void Plant(Frame f, in AnimState s, float t)
        {
            var def = s.job?.def;
            if (def == JobDefOf.Sow || def == JobDefOf.PlantSeed)
            {
                float dig = Curves.Hump(Curves.Cycle(t, 48f), 0.1f, 0.6f);
                Vector3 pos = f.At(0.34f, -0.1f - 0.06f * dig, 0.05f, Step * 6);
                Draw(Trowel, pos, f.Angle(150f - 25f * dig, 180f), 0.36f, f.Flip);
            }
            else
            {
                float p = Curves.Cycle(t, 44f);
                float swing = p < 0.7f ? Curves.Smooth(p / 0.7f) : 1f - (p - 0.7f) / 0.3f;
                Vector3 pos = f.At(0.36f, -0.06f, 0.06f, Step * 6);
                Draw(Sickle, pos, f.Angle(Mathf.Lerp(20f, 140f, swing), Mathf.Lerp(-60f, 60f, swing)), 0.42f, f.Flip);
            }
        }

        private static void Sweep(Frame f, float t)
        {
            float sw = Mathf.Sin(Curves.Cycle(t, 44f) * Curves.TwoPi);
            Vector3 pos = f.At(0.3f, -0.12f, 0.04f, Step * 6) + (f.Horizontal ? f.fwd * (0.06f * sw) : new Vector3(0.12f * sw, 0f, 0f));
            Draw(Broom, pos, f.Angle(-20f + 25f * sw, 25f * sw), 0.55f, f.Flip);
            if (Mathf.Abs(sw) > 0.85f)
                DrawFaded(Dust, pos + new Vector3(0f, Step, -0.2f), t * 3f, 0.25f, 0.35f * (Mathf.Abs(sw) - 0.85f) / 0.15f);
        }

        private static void Write(Frame f, float t)
        {
            Vector3 paper = f.At(0.36f, -0.06f, 0f, Step);
            Draw(Paper, paper, f.Angle(-8f, 6f), 0.3f);
            float think = Curves.Hump(Curves.Cycle(t, 220f), 0.78f, 0.97f);
            float line = Curves.Cycle(t, 40f);
            float row = Mathf.Floor(t / 40f % 4f);
            Vector3 nib = paper + new Vector3(Mathf.Lerp(-0.07f, 0.07f, line), Step * 4, 0.06f - row * 0.03f + Mathf.Sin(t * 1.7f) * 0.008f);
            nib += new Vector3(0f, 0f, 0.1f * think);
            Draw(Quill, Tip(nib, 25f, 0.13f), 25f - 15f * think, 0.3f);
        }

        private static void Tend(Frame f, in AnimState s, float t)
        {
            float a = Curves.Cycle(t, 50f) * Curves.TwoPi;
            Vector3 pos = f.At(0.36f, -0.04f, 0f, Step * 6) + new Vector3(Mathf.Cos(a) * 0.05f, 0f, Mathf.Sin(a) * 0.03f);
            // Use the real medicine when the doctor has one, otherwise a bandage roll.
            var med = MatOf(s.subject);
            if (med != null) Draw(med, pos, Mathf.Sin(a) * 15f, 0.26f);
            else Draw(Bandage, pos, f.Angle(Mathf.Sin(a) * 20f, Mathf.Sin(a) * 20f), 0.28f, f.Flip);
        }

        // ------------------------------------------------------------------ reading and games

        private static void Read(Pawn pawn, Frame f, in AnimState s, float t)
        {
            var thing = s.subject;
            Vector3 at;
            float size = 0.38f;
            if (thing != null && pawn.carryTracker?.CarriedThing == thing)
            {
                at = pawn.DrawPos;
                PawnRenderUtility.CalculateCarriedDrawPos(pawn, thing, ref at, out _);
            }
            else if (thing != null && thing.Spawned)
            {
                at = thing.DrawPos;
            }
            else
            {
                // Held from the inventory: vanilla doesn't draw it, so we draw the real book/document.
                at = f.At(0.22f, -0.04f, 0f, Step);
                Material mat = null;
                if (thing is Book book && bookOpenGraphic != null)
                {
                    try { mat = bookOpenGraphic(book)?.MatSingle; } catch { mat = null; }
                }
                Draw(mat ?? MatOf(thing) ?? Paper, at, 0f, size);
            }
            at.y = Mathf.Max(at.y, f.origin.y) + Step * 4;

            // Turn a page every so often: the page swings over the spine and lands on the other side.
            float p = Curves.Cycle(t, 170f);
            if (p < 0.13f)
            {
                float q = p / 0.13f;
                float c = Mathf.Cos(q * Mathf.PI);
                var m = Matrix4x4.TRS(at + new Vector3(0f, 0f, 0.02f * Mathf.Sin(q * Mathf.PI)), Quaternion.identity,
                    new Vector3(size * Mathf.Abs(c), 1f, size * 0.9f));
                Graphics.DrawMesh(c >= 0f ? MeshPool.plane10 : MeshPool.plane10Flip, m, Page, 0);
            }
        }

        private static void BoardGame(Frame f, float t)
        {
            float p = Curves.Cycle(t, 160f);
            float move = Curves.Smooth((p - 0.12f) / 0.24f);
            float lift = Curves.Hump(p, 0.12f, 0.36f);
            int turn = (int)(t / 160f) % 3;
            Vector3 lateral = f.Horizontal ? new Vector3(0f, 0f, (turn - 1) * 0.08f) : new Vector3((turn - 1) * 0.08f, 0f, 0f);
            Vector3 pos = f.At(0.5f + 0.12f * move, 0.04f * lift, 0f, Step * 4) + lateral;
            Draw(GamePiece, pos, 0f, 0.24f + 0.04f * lift);
        }

        private static void Cards(Frame f, float t)
        {
            float bob = Mathf.Sin(t / 130f * Curves.TwoPi) * 0.01f;
            Vector3 fan = f.At(0.2f, 0.02f + bob, 0.04f, Step * 4);
            Draw(CardFan, fan, f.Angle(25f, -10f), 0.34f, f.Flip);
            float p = Curves.Cycle(t, 140f);
            if (p < 0.15f)
            {
                float q = p / 0.15f;
                Vector3 pos = Vector3.Lerp(fan, f.At(0.62f, -0.04f, 0f, Step * 5), q) + new Vector3(0f, 0f, 0.12f * Mathf.Sin(q * Mathf.PI));
                Draw(Card, pos, q * 360f, 0.24f);
            }
        }

        private static void Throw(Frame f, in AnimState s, float t)
        {
            if (!(s.driver is JobDriver_PlayHorseshoes)) return;
            float p = Curves.Cycle(t, 120f);
            if (p > 0.42f) return; // the horseshoe has left the hand
            float wind = Curves.Hump(p, 0f, 0.38f);
            Vector3 pos = f.At(0.12f - 0.22f * wind, 0.06f + 0.1f * wind, 0.14f, Step * 4);
            Draw(Horseshoe, pos, f.Angle(-40f * wind, 20f * wind), 0.24f, f.Flip);
        }

        // ------------------------------------------------------------------ apparel and equipment

        private static void Apparel(Frame f, in AnimState s, float t, bool putOn)
        {
            var mat = MatOf(s.subject);
            if (mat == null) return;
            float pr = putOn ? s.progress : 1f - s.progress;
            if (pr > 0.93f) return;
            float drop = Curves.Smooth(pr / 0.93f);
            Vector3 pos = f.origin + new Vector3(Mathf.Sin(t * 0.9f) * 0.03f * (1f - drop), Step * 6, Mathf.Lerp(0.78f, 0.08f, drop));
            Draw(mat, pos, Mathf.Sin(t * 0.9f) * 9f * (1f - drop), Mathf.Lerp(0.72f, 0.58f, drop));
        }

        private static void Equip(Pawn pawn, Frame f, in AnimState s)
        {
            float p = s.progress;
            var mat = MatOf(s.subject);
            if (mat != null && !pawn.Drafted)
            {
                float hop = Curves.Hump(p, 0f, 0.55f);
                Vector3 pos = f.origin + new Vector3(0f, Step * 6, 0.45f + 0.25f * hop);
                Draw(mat, pos, 360f * Curves.Smooth(p / 0.8f) * (f.side < 0f ? -1f : 1f), 0.7f);
            }
            if (p > 0.6f)
            {
                float q = (p - 0.6f) / 0.4f;
                DrawFaded(Spark, f.origin + new Vector3(0.22f, Step * 8, 0.6f), q * 90f, 0.25f * Mathf.Sin(q * Mathf.PI), 1f - q);
            }
        }
    }
}
