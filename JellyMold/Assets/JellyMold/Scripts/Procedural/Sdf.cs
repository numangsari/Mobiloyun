using System;

namespace JellyMold.Procedural
{
    /// <summary>
    /// 2D signed distance functions (negative inside) and helpers.
    /// Formulas follow the well-known public-domain shapes described by Inigo Quilez.
    /// </summary>
    public static class Sdf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Anti-aliased coverage of a distance field, <paramref name="aa"/> = one pixel.</summary>
        public static float Fill(float d, float aa) => Clamp01(0.5f - d / aa);

        /// <summary>Soft coverage for glows and shadows.</summary>
        public static float Soft(float d, float feather) => 1f - SmoothStep(-feather, feather, d);

        public static float Circle(float x, float y, float r) => Length(x, y) - r;

        public static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            float qx = Math.Abs(x) - hx + r, qy = Math.Abs(y) - hy + r;
            return Length(Math.Max(qx, 0f), Math.Max(qy, 0f)) + Math.Min(Math.Max(qx, qy), 0f) - r;
        }

        public static float Ellipse(float x, float y, float rx, float ry)
        {
            // Cheap approximation, good enough for soft highlights.
            float k = Length(x / rx, y / ry);
            return (k - 1f) * Math.Min(rx, ry);
        }

        public static float Segment(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Length(pax - bax * h, pay - bay * h) - r;
        }

        /// <summary>Stroke of a circle arc between angles a0..a1 (radians, counter-clockwise).</summary>
        public static float Arc(float x, float y, float radius, float a0, float a1, float thickness)
        {
            float a = (float)Math.Atan2(y, x);
            if (InAngle(a, a0, a1)) return Math.Abs(Length(x, y) - radius) - thickness;
            float e0x = (float)Math.Cos(a0) * radius, e0y = (float)Math.Sin(a0) * radius;
            float e1x = (float)Math.Cos(a1) * radius, e1y = (float)Math.Sin(a1) * radius;
            return Math.Min(Length(x - e0x, y - e0y), Length(x - e1x, y - e1y)) - thickness;
        }

        private static bool InAngle(float a, float a0, float a1)
        {
            const float Tau = (float)(Math.PI * 2);
            float span = ((a1 - a0) % Tau + Tau) % Tau;
            float rel = ((a - a0) % Tau + Tau) % Tau;
            return rel <= span;
        }

        public static float Triangle(float px, float py, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float e0x = x1 - x0, e0y = y1 - y0, e1x = x2 - x1, e1y = y2 - y1, e2x = x0 - x2, e2y = y0 - y2;
            float v0x = px - x0, v0y = py - y0, v1x = px - x1, v1y = py - y1, v2x = px - x2, v2y = py - y2;
            float h0 = Clamp01((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y));
            float h1 = Clamp01((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y));
            float h2 = Clamp01((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y));
            float d0 = Sq(v0x - e0x * h0, v0y - e0y * h0);
            float d1 = Sq(v1x - e1x * h1, v1y - e1y * h1);
            float d2 = Sq(v2x - e2x * h2, v2y - e2y * h2);
            float s = Math.Sign(e0x * e2y - e0y * e2x);
            float c0 = s * (v0x * e0y - v0y * e0x), c1 = s * (v1x * e1y - v1y * e1x), c2 = s * (v2x * e2y - v2y * e2x);
            float d = (float)Math.Sqrt(Math.Min(d0, Math.Min(d1, d2)));
            return Math.Min(c0, Math.Min(c1, c2)) > 0 ? -d : d;
        }

        /// <summary>Heart roughly spanning x ±0.6, y 0..1.05 at scale 1 (before the caller's transform).</summary>
        public static float Heart(float x, float y)
        {
            x = Math.Abs(x);
            if (y + x > 1f) return Length(x - 0.25f, y - 0.75f) - 0.35355339f;
            float a = Sq(x, y - 1f);
            float m = 0.5f * Math.Max(x + y, 0f);
            float b = Sq(x - m, y - m);
            return (float)Math.Sqrt(Math.Min(a, b)) * Math.Sign(x - y);
        }

        /// <summary>Five-pointed star with outer radius r and inner ratio rf.</summary>
        public static float Star5(float x, float y, float r, float rf)
        {
            const float k1x = 0.809016994375f, k1y = -0.587785252292f;
            const float k2x = -k1x, k2y = k1y;
            x = Math.Abs(x);
            float d1 = 2f * Math.Max(k1x * x + k1y * y, 0f);
            x -= d1 * k1x;
            y -= d1 * k1y;
            float d2 = 2f * Math.Max(k2x * x + k2y * y, 0f);
            x -= d2 * k2x;
            y -= d2 * k2y;
            x = Math.Abs(x);
            y -= r;
            float bax = rf * -k1y, bay = rf * k1x - 1f;
            float h = Clamp((x * bax + y * bay) / (bax * bax + bay * bay), 0f, r);
            return Length(x - bax * h, y - bay * h) * Math.Sign(y * bax - x * bay);
        }

        /// <summary>Polynomial smooth minimum for organic unions.</summary>
        public static float SmoothMin(float a, float b, float k)
        {
            float h = Clamp01(0.5f + 0.5f * (b - a) / k);
            return Lerp(b, a, h) - k * h * (1f - h);
        }

        public static void Rotate(ref float x, ref float y, float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            float nx = c * x - s * y, ny = s * x + c * y;
            x = nx;
            y = ny;
        }

        private static float Sq(float x, float y) => x * x + y * y;
    }
}
