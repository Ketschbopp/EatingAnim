using UnityEngine;
using Verse;

namespace EatingAnim
{
    /// <summary>Small offsets applied on top of vanilla's pawn transform.</summary>
    public struct AnimPose
    {
        public Vector3 body;     // world offset of the whole pawn (x right, z up the screen)
        public float bodyAngle;  // degrees, clockwise
        public Vector3 head;     // head offset relative to the body
        public float headAngle;
    }

    /// <summary>Curves shared by poses and props so both stay in sync.</summary>
    public static class Curves
    {
        public const float TwoPi = Mathf.PI * 2f;

        public static float Cycle(float t, float period) => (t % period) / period;

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>0 outside [a,b], a single smooth hump inside.</summary>
        public static float Hump(float p, float a, float b) =>
            p <= a || p >= b ? 0f : Mathf.Sin((p - a) / (b - a) * Mathf.PI);

        /// <summary>Lift to the mouth, hold, lower: a bite.</summary>
        public static float Bite(float p)
        {
            if (p < 0.22f) return Smooth(p / 0.22f);
            if (p < 0.32f) return 1f;
            if (p < 0.48f) return 1f - Smooth((p - 0.32f) / 0.16f);
            return 0f;
        }

        /// <summary>Chewing wobble after each bite.</summary>
        public static float Chew(float p) =>
            p > 0.3f && p < 0.86f ? Mathf.Sin((p - 0.3f) / 0.56f * 3f * TwoPi) : 0f;

        /// <summary>Raise slowly, strike fast, recoil. Returns raise (0..1) and impact (0..1).</summary>
        public static void Strike(float p, float strikeAt, out float raise, out float impact)
        {
            const float strikeLen = 0.09f;
            if (p < strikeAt) raise = Smooth(p / strikeAt);
            else if (p < strikeAt + strikeLen) raise = 1f - (p - strikeAt) / strikeLen;
            else raise = 0f;
            impact = Hump(p, strikeAt + strikeLen * 0.6f, strikeAt + strikeLen + 0.14f);
        }

        public static Vector3 Forward(Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0: return new Vector3(0f, 0f, 1f);
                case 1: return new Vector3(1f, 0f, 0f);
                case 2: return new Vector3(0f, 0f, -1f);
                default: return new Vector3(-1f, 0f, 0f);
            }
        }

        /// <summary>+1 facing east, -1 facing west, 0 otherwise: the sign for "lean forward" rotations.</summary>
        public static float Side(Rot4 rot) => rot.AsInt == 1 ? 1f : rot.AsInt == 3 ? -1f : 0f;
    }

    public static class PoseUtility
    {
        /// <summary>Length of one pour-and-swirl cycle at the drug lab, shared with the props.</summary>
        public const float ChemCycle = 240f;

        public static AnimPose For(in AnimState s)
        {
            var pose = new AnimPose();
            Vector3 fwd = Curves.Forward(s.rot);
            float side = Curves.Side(s.rot);
            Vector3 down = new Vector3(0f, 0f, -1f);
            float t = s.t;
            float p;

            switch (s.kind)
            {
                case AnimKind.EatTable:
                {
                    p = Curves.Cycle(t, 84f);
                    float bite = Curves.Bite(p);
                    pose.body = fwd * (0.025f + 0.02f * bite);
                    pose.bodyAngle = side * (2f + 3f * bite);
                    pose.head = down * (0.014f * bite) + new Vector3(0f, 0f, 0.006f * Curves.Chew(p));
                    pose.headAngle = side * 7f * bite;
                    break;
                }
                case AnimKind.EatGround:
                {
                    p = Curves.Cycle(t, 96f);
                    float bite = Curves.Bite(p);
                    float rock = Mathf.Sin(t / 60f * Curves.TwoPi);
                    pose.body = down * 0.05f + fwd * 0.02f + new Vector3(0f, 0f, 0.004f * rock);
                    pose.bodyAngle = side * 8f;
                    pose.head = down * (0.02f * bite) + new Vector3(0f, 0f, 0.006f * Curves.Chew(p));
                    pose.headAngle = side * 9f * bite;
                    break;
                }
                case AnimKind.EatAnimal:
                {
                    float g = 0.5f + 0.5f * Mathf.Sin(Curves.Cycle(t, 70f) * Curves.TwoPi);
                    pose.body = fwd * (0.035f * g) + down * (0.015f * g);
                    pose.bodyAngle = side * 4f * g;
                    break;
                }
                case AnimKind.Chem:
                {
                    p = Curves.Cycle(t, ChemCycle);
                    float pour = Curves.Hump(p, 0.16f, 0.58f);
                    float swirl = Curves.Hump(p, 0.66f, 0.96f) * Mathf.Sin(t * 0.5f);
                    pose.body = fwd * (0.018f + 0.016f * pour) + new Vector3(0.006f * swirl, 0f, 0f);
                    pose.bodyAngle = side * 3f * pour + 1.5f * swirl;
                    pose.head = down * (0.01f + 0.008f * pour);
                    pose.headAngle = side * (4f + 5f * pour);
                    break;
                }
                case AnimKind.Cook:
                {
                    float period = 120f;
                    float sway = Mathf.Sin(Curves.Cycle(t, period) * Curves.TwoPi);
                    float stir = Mathf.Sin(Curves.Cycle(t, 40f) * Curves.TwoPi);
                    pose.body = new Vector3(sway * 0.012f, 0f, 0f) + fwd * 0.015f;
                    pose.bodyAngle = 1.5f * sway;
                    pose.head = down * 0.01f;
                    pose.headAngle = side * 5f + stir * 1.5f;
                    break;
                }
                case AnimKind.Butcher:
                case AnimKind.Hammer:
                case AnimKind.Construct:
                case AnimKind.Mine:
                {
                    float period, strikeAt, lean, tilt;
                    switch (s.kind)
                    {
                        case AnimKind.Butcher: period = 54f; strikeAt = 0.6f; lean = 0.03f; tilt = 6f; break;
                        case AnimKind.Mine: period = 64f; strikeAt = 0.58f; lean = 0.045f; tilt = 9f; break;
                        case AnimKind.Construct: period = 40f; strikeAt = 0.55f; lean = 0.025f; tilt = 5f; break;
                        default: period = 34f; strikeAt = 0.55f; lean = 0.018f; tilt = 3.5f; break;
                    }
                    Curves.Strike(Curves.Cycle(t, period), strikeAt, out float raise, out float impact);
                    pose.body = fwd * (lean * impact - lean * 0.6f * raise) + down * (0.02f * impact);
                    pose.bodyAngle = side * (tilt * impact - tilt * 0.7f * raise);
                    pose.head = down * (0.006f + 0.01f * impact);
                    pose.headAngle = side * 5f * impact;
                    break;
                }
                case AnimKind.Sew:
                {
                    float pull = Curves.Hump(Curves.Cycle(t, 30f), 0.4f, 0.9f);
                    pose.body = fwd * 0.01f;
                    pose.head = down * (0.014f - 0.005f * pull);
                    pose.headAngle = side * (7f - 3f * pull);
                    break;
                }
                case AnimKind.Chisel:
                {
                    Curves.Strike(Curves.Cycle(t, 22f), 0.55f, out _, out float impact);
                    pose.body = fwd * (0.012f + 0.006f * impact);
                    pose.head = down * 0.012f;
                    pose.headAngle = side * 6f;
                    break;
                }
                case AnimKind.Plant:
                {
                    float dig = Curves.Hump(Curves.Cycle(t, 48f), 0.1f, 0.6f);
                    pose.body = down * 0.06f + fwd * (0.02f + 0.02f * dig);
                    pose.bodyAngle = side * (8f + 4f * dig);
                    pose.head = down * (0.01f * dig);
                    break;
                }
                case AnimKind.Sweep:
                {
                    float sw = Mathf.Sin(Curves.Cycle(t, 44f) * Curves.TwoPi);
                    pose.body = s.rot.IsHorizontal ? fwd * (0.012f * sw) : new Vector3(-0.012f * sw, 0f, 0f);
                    pose.bodyAngle = 2f * sw;
                    pose.head = down * 0.006f;
                    break;
                }
                case AnimKind.Write:
                {
                    float think = Curves.Hump(Curves.Cycle(t, 220f), 0.78f, 0.97f);
                    pose.body = new Vector3(0f, 0f, 0.004f * Mathf.Sin(t / 150f * Curves.TwoPi));
                    pose.head = down * (0.012f - 0.026f * think);
                    pose.headAngle = side * (6f - 11f * think) + 4f * think * (1f - Mathf.Abs(side));
                    break;
                }
                case AnimKind.Tend:
                {
                    float w = Mathf.Sin(Curves.Cycle(t, 50f) * Curves.TwoPi);
                    pose.body = down * 0.05f + fwd * 0.03f;
                    pose.bodyAngle = side * 6f;
                    pose.head = down * (0.008f + 0.004f * w);
                    pose.headAngle = side * 5f;
                    break;
                }
                case AnimKind.Read:
                {
                    float scan = Mathf.Sin(Curves.Cycle(t, 90f) * Curves.TwoPi);
                    float flip = Curves.Hump(Curves.Cycle(t, 170f), 0f, 0.14f);
                    pose.body = new Vector3(0f, 0f, 0.004f * Mathf.Sin(t / 150f * Curves.TwoPi));
                    pose.head = down * (0.012f + 0.004f * flip);
                    pose.headAngle = side * 4f + scan * 2f;
                    break;
                }
                case AnimKind.BoardGame:
                {
                    float move = Curves.Hump(Curves.Cycle(t, 160f), 0.1f, 0.38f);
                    pose.body = fwd * (0.03f * move);
                    pose.head = down * (0.01f * move);
                    pose.headAngle = Mathf.Sin(t / 220f * Curves.TwoPi) * 5f * (1f - move);
                    break;
                }
                case AnimKind.Cards:
                {
                    float deal = Curves.Hump(Curves.Cycle(t, 140f), 0f, 0.15f);
                    pose.body = fwd * (0.02f * deal) + new Vector3(0f, 0f, 0.004f * Mathf.Sin(t / 130f * Curves.TwoPi));
                    pose.bodyAngle = side * 3f * deal;
                    pose.headAngle = Mathf.Sin(t / 260f * Curves.TwoPi) * 3f;
                    break;
                }
                case AnimKind.Throw:
                {
                    p = Curves.Cycle(t, 120f);
                    float wind = Curves.Hump(p, 0f, 0.38f);
                    float thr = Curves.Hump(p, 0.34f, 0.52f);
                    pose.body = fwd * (0.06f * thr - 0.04f * wind);
                    pose.bodyAngle = side * (10f * thr - 8f * wind) + (1f - Mathf.Abs(side)) * (4f * thr - 3f * wind);
                    pose.headAngle = side * (-4f * wind);
                    break;
                }
                case AnimKind.Wear:
                case AnimKind.Unwear:
                {
                    float env = Mathf.Sqrt(Curves.Hump(s.progress, 0f, 1f));
                    float wiggle = Mathf.Sin(t * 0.9f);
                    float hop = Mathf.Abs(Mathf.Sin(t * 0.35f));
                    pose.body = new Vector3(wiggle * 0.015f * env, 0f, hop * 0.03f * env);
                    pose.bodyAngle = wiggle * 5f * env;
                    pose.headAngle = -wiggle * 4f * env;
                    break;
                }
                case AnimKind.Equip:
                {
                    float hop = Curves.Hump(s.progress, 0f, 0.55f);
                    pose.body = new Vector3(0f, 0f, 0.07f * hop);
                    pose.bodyAngle = Mathf.Sin(s.progress * Curves.TwoPi) * 4f * (1f - s.progress);
                    pose.head = new Vector3(0f, 0f, 0.01f * hop);
                    break;
                }
            }

            float k = EatingAnimMod.Settings.intensity;
            pose.body *= k;
            pose.bodyAngle *= k;
            pose.head *= k;
            pose.headAngle *= k;
            return pose;
        }
    }
}
