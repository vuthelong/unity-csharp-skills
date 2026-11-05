#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ParticleVfx.Gen.Editor
{
    public readonly struct FloatRange
    {
        public readonly float Min;
        public readonly float Max;

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public bool IsConstant => Mathf.Approximately(Min, Max);

        public ParticleSystem.MinMaxCurve ToCurve(float scale = 1f)
        {
            return IsConstant
                ? new ParticleSystem.MinMaxCurve(Min * scale)
                : new ParticleSystem.MinMaxCurve(Min * scale, Max * scale);
        }

        public override string ToString()
        {
            return IsConstant
                ? Min.ToString("0.###", CultureInfo.InvariantCulture)
                : "[" + Min.ToString("0.###", CultureInfo.InvariantCulture) + ", " + Max.ToString("0.###", CultureInfo.InvariantCulture) + "]";
        }
    }

    public readonly struct CurveKey
    {
        public readonly float Time;
        public readonly float Value;

        public CurveKey(float time, float value)
        {
            Time = time;
            Value = value;
        }
    }

    public readonly struct ColorKey
    {
        public readonly float Time;
        public readonly Color Color;

        public ColorKey(float time, Color color)
        {
            Time = time;
            Color = color;
        }
    }

    public sealed class BurstSpec
    {
        public float Time;
        public FloatRange Count;
        public int Cycles = 1;
        public float Interval = 0.01f;
    }

    public sealed class ShapeSpec
    {
        public string Type = "point";
        public float Radius = 0.01f;
        public float RadiusThickness = 1f;
        public float Arc = 360f;
        public float Angle = 25f;
        public float? Dir2D;
        public Vector3 Position = Vector3.zero;
        public Vector3 Rotation = Vector3.zero;
        public Vector3 Scale = Vector3.one;
        public float RandomDirection;
        public bool AlignToDirection;
        public bool ArcLoop;
    }

    public sealed class VelocitySpec
    {
        public FloatRange X;
        public FloatRange Y;
        public FloatRange Radial;
        public FloatRange OrbitalZ;
        public bool World;
        public List<CurveKey> SpeedOverLife;
    }

    public sealed class NoiseSpec
    {
        public float Strength = 0.5f;
        public float Frequency = 1f;
        public float Scroll;
    }

    public sealed class SheetSpec
    {
        public int TilesX = 1;
        public int TilesY = 1;
        public float Fps;
        public float Cycles = 1f;
        public bool RandomStart;
        public bool RandomRow;
    }

    public sealed class StretchSpec
    {
        public float SpeedScale = 0.1f;
        public float LengthScale = 1f;
    }

    public sealed class LayerSpec
    {
        public string Name;
        public string Texture;
        public string Material;
        public string Blend = "add";
        public string Sorting;
        public int Order;
        public Vector3 Position = Vector3.zero;

        public float? Duration;
        public bool? Loop;
        public FloatRange Delay;
        public int? MaxParticles;
        public bool World;
        public bool Prewarm;

        public FloatRange Lifetime = new FloatRange(1f, 1f);
        public FloatRange Speed;
        public FloatRange Size = new FloatRange(1f, 1f);
        public float Aspect = 1f;
        public FloatRange Rotation;
        public FloatRange Spin;
        public float Flip;
        public Color ColorA = Color.white;
        public Color? ColorB;
        public float Gravity;
        public float Drag;

        public float Rate;
        public float RateOverDistance;
        public List<BurstSpec> Bursts = new List<BurstSpec>();

        public ShapeSpec Shape = new ShapeSpec();
        public VelocitySpec Velocity;
        public NoiseSpec Noise;
        public SheetSpec Sheet;
        public List<CurveKey> SizeOverLife;
        public List<ColorKey> ColorOverLife;
        public List<CurveKey> AlphaOverLife;

        public string Render = "billboard";
        public StretchSpec Stretch;
    }

    public sealed class VfxSpec
    {
        public const string DefaultMaterialFolder = "Assets/VFX/Materials/Gen";
        public const string DefaultSorting = "Default";

        public string SourcePath;
        public string Name;
        public string Out;
        public string MaterialFolder = DefaultMaterialFolder;
        public string Sorting = DefaultSorting;
        public float Duration = 1f;
        public bool Loop;
        public float? Despawn;
        public float? HitTime;
        public float ReferenceHeight = -1f;
        public string Notes;
        public List<LayerSpec> Layers = new List<LayerSpec>();
    }

    public static class VfxSpecParser
    {
        #region Public API

        public static VfxSpec Parse(string json, string sourcePath, List<string> errors)
        {
            var spec = new VfxSpec { SourcePath = sourcePath };
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                errors.Add("JSON: " + e.Message);
                return null;
            }

            var r = new Reader(root, "root", errors);
            spec.Name = r.String("name", null);
            spec.Out = r.String("out", null);
            spec.MaterialFolder = r.String("materialFolder", spec.MaterialFolder).TrimEnd('/');
            spec.Sorting = r.String("sorting", spec.Sorting);
            spec.Duration = r.Float("duration", spec.Duration);
            spec.Loop = r.Bool("loop", false);
            spec.Despawn = r.OptFloat("despawn");
            spec.HitTime = r.OptFloat("hitTime");
            spec.ReferenceHeight = r.Float("referenceHeight", spec.ReferenceHeight);
            spec.Notes = r.String("notes", null);
            var layers = r.Array("layers");
            if (layers != null)
            {
                foreach (var token in layers)
                {
                    if (token is JObject o)
                        spec.Layers.Add(ParseLayer(o, errors));
                    else
                        errors.Add("layers: every entry must be an object");
                }
            }

            r.Finish();
            if (string.IsNullOrEmpty(spec.Name)) errors.Add("root: 'name' is required");
            if (string.IsNullOrEmpty(spec.Out) || !spec.Out.StartsWith("Assets/") || !spec.Out.EndsWith(".prefab"))
                errors.Add("root: 'out' must be an Assets/...prefab path");
            if (!spec.MaterialFolder.StartsWith("Assets/"))
                errors.Add("root: 'materialFolder' must be an Assets/... folder");
            if (spec.Layers.Count == 0) errors.Add("root: at least one layer is required");
            foreach (var l in spec.Layers)
                if (string.IsNullOrEmpty(l.Sorting))
                    l.Sorting = spec.Sorting;
            var dupes = spec.Layers.GroupBy(l => l.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0) errors.Add("layer names must be unique: " + string.Join(", ", dupes));
            return spec;
        }

        #endregion

        #region Layer

        private static LayerSpec ParseLayer(JObject o, List<string> errors)
        {
            var l = new LayerSpec();
            var r = new Reader(o, "layer", errors);
            l.Name = r.String("name", null);
            r.Where = "layer '" + (l.Name ?? "?") + "'";
            if (string.IsNullOrEmpty(l.Name)) errors.Add(r.Where + ": 'name' is required");

            l.Texture = r.String("texture", null);
            l.Material = r.String("material", null);
            l.Blend = r.Enum("blend", l.Blend, "add", "alpha");
            l.Sorting = r.String("sorting", null);
            l.Order = (int)r.Float("order", 0);
            l.Position = r.Vector("position", l.Position);
            l.Duration = r.OptFloat("duration");
            l.Loop = r.OptBool("loop");
            l.Delay = r.Range("delay", default);
            var max = r.OptFloat("maxParticles");
            if (max.HasValue) l.MaxParticles = Mathf.RoundToInt(max.Value);
            l.World = r.Enum("space", "local", "local", "world") == "world";
            l.Prewarm = r.Bool("prewarm", false);

            l.Lifetime = r.Range("lifetime", l.Lifetime);
            l.Speed = r.Range("speed", default);
            l.Size = r.Range("size", l.Size);
            l.Aspect = r.Float("aspect", 1f);
            l.Rotation = r.Range("rotation", default);
            l.Spin = r.Range("spin", default);
            l.Flip = r.Float("flip", 0f);
            ParseColors(r, l);
            l.Gravity = r.Float("gravity", 0f);
            l.Drag = r.Float("drag", 0f);

            l.Rate = r.Float("rate", 0f);
            l.RateOverDistance = r.Float("rateOverDistance", 0f);
            var bursts = r.Array("bursts");
            if (bursts != null)
                foreach (var b in bursts)
                    l.Bursts.Add(ParseBurst(b, r.Where, errors));

            var shape = r.Object("shape");
            if (shape != null) l.Shape = ParseShape(shape, r.Where, errors);
            var velocity = r.Object("velocity");
            if (velocity != null) l.Velocity = ParseVelocity(velocity, r.Where, errors);
            var noise = r.Object("noise");
            if (noise != null)
            {
                var nr = new Reader(noise, r.Where + ".noise", errors);
                l.Noise = new NoiseSpec
                {
                    Strength = nr.Float("strength", 0.5f),
                    Frequency = nr.Float("frequency", 1f),
                    Scroll = nr.Float("scroll", 0f)
                };
                nr.Finish();
            }

            var sheet = r.Object("sheet");
            if (sheet != null)
            {
                var sr = new Reader(sheet, r.Where + ".sheet", errors);
                var tiles = sr.Vector("tiles", new Vector3(1, 1, 0));
                l.Sheet = new SheetSpec
                {
                    TilesX = Mathf.Max(1, Mathf.RoundToInt(tiles.x)),
                    TilesY = Mathf.Max(1, Mathf.RoundToInt(tiles.y)),
                    Fps = sr.Float("fps", 0f),
                    Cycles = sr.Float("cycles", 1f),
                    RandomStart = sr.Bool("randomStart", false),
                    RandomRow = sr.Bool("randomRow", false)
                };
                sr.Finish();
            }

            l.SizeOverLife = r.Curve("sizeOverLife");
            l.AlphaOverLife = r.Curve("alphaOverLife");
            l.ColorOverLife = r.ColorCurve("colorOverLife");

            l.Render = r.Enum("render", l.Render, "billboard", "stretch", "local", "world");
            var stretch = r.Object("stretch");
            if (stretch != null)
            {
                var sr = new Reader(stretch, r.Where + ".stretch", errors);
                l.Stretch = new StretchSpec { SpeedScale = sr.Float("speedScale", 0.1f), LengthScale = sr.Float("lengthScale", 1f) };
                sr.Finish();
            }

            r.Finish();
            if (string.IsNullOrEmpty(l.Texture) == string.IsNullOrEmpty(l.Material))
                errors.Add(r.Where + ": set exactly one of 'texture' or 'material'");
            if (l.Rate <= 0f && l.RateOverDistance <= 0f && l.Bursts.Count == 0)
                errors.Add(r.Where + ": emits nothing (no rate, rateOverDistance or bursts)");
            return l;
        }

        private static void ParseColors(Reader r, LayerSpec l)
        {
            var token = r.Raw("color");
            if (token == null) return;
            if (token.Type == JTokenType.String)
            {
                l.ColorA = r.ToColor(token, "color");
            }
            else if (token is JArray a && a.Count == 2)
            {
                l.ColorA = r.ToColor(a[0], "color");
                l.ColorB = r.ToColor(a[1], "color");
            }
            else
            {
                r.Error("color must be \"#rrggbb[aa]\" or [\"#a\", \"#b\"]");
            }
        }

        private static BurstSpec ParseBurst(JToken token, string where, List<string> errors)
        {
            var burst = new BurstSpec();
            if (token is JArray a && a.Count >= 2)
            {
                burst.Time = a[0].Value<float>();
                burst.Count = Reader.ToRange(a[1]);
                if (a.Count >= 3) burst.Cycles = a[2].Value<int>();
                if (a.Count >= 4) burst.Interval = a[3].Value<float>();
                return burst;
            }

            if (token is JObject o)
            {
                var r = new Reader(o, where + ".bursts", errors);
                burst.Time = r.Float("t", 0f);
                burst.Count = r.Range("n", new FloatRange(1, 1));
                burst.Cycles = (int)r.Float("cycles", 1);
                burst.Interval = r.Float("interval", 0.01f);
                r.Finish();
                return burst;
            }

            errors.Add(where + ": a burst is [t, n] / [t, [nMin, nMax]] / [t, n, cycles, interval] or {t, n, cycles, interval}");
            return burst;
        }

        private static ShapeSpec ParseShape(JObject o, string where, List<string> errors)
        {
            var r = new Reader(o, where + ".shape", errors);
            var s = new ShapeSpec
            {
                Type = r.Enum("type", "point", "point", "circle", "cone", "sphere", "hemisphere", "edge", "box", "rect"),
                Radius = r.Float("radius", 0.01f),
                RadiusThickness = r.Float("thickness", 1f),
                Arc = r.Float("arc", 360f),
                Angle = r.Float("angle", 25f),
                Dir2D = r.OptFloat("dir"),
                Position = r.Vector("position", Vector3.zero),
                Rotation = r.Vector("rotation", Vector3.zero),
                Scale = r.Vector("scale", Vector3.one),
                RandomDirection = r.Float("randomDirection", 0f),
                AlignToDirection = r.Bool("alignToDirection", false),
                ArcLoop = r.Bool("arcLoop", false)
            };
            r.Finish();
            return s;
        }

        private static VelocitySpec ParseVelocity(JObject o, string where, List<string> errors)
        {
            var r = new Reader(o, where + ".velocity", errors);
            var v = new VelocitySpec
            {
                X = r.Range("x", default),
                Y = r.Range("y", default),
                Radial = r.Range("radial", default),
                OrbitalZ = r.Range("orbital", default),
                World = r.Enum("space", "local", "local", "world") == "world",
                SpeedOverLife = r.Curve("speedOverLife")
            };
            r.Finish();
            return v;
        }

        #endregion

        #region Reader

        private sealed class Reader
        {
            private readonly JObject _o;
            private readonly List<string> _errors;
            private readonly HashSet<string> _used = new HashSet<string>();
            public string Where;

            public Reader(JObject o, string where, List<string> errors)
            {
                _o = o;
                Where = where;
                _errors = errors;
            }

            public void Error(string message)
            {
                _errors.Add(Where + ": " + message);
            }

            public JToken Raw(string key)
            {
                _used.Add(key);
                var t = _o[key];
                return t == null || t.Type == JTokenType.Null ? null : t;
            }

            public string String(string key, string fallback)
            {
                var t = Raw(key);
                return t == null ? fallback : t.Value<string>();
            }

            public float Float(string key, float fallback)
            {
                return OptFloat(key) ?? fallback;
            }

            public float? OptFloat(string key)
            {
                var t = Raw(key);
                if (t == null) return null;
                if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return t.Value<float>();
                Error("'" + key + "' must be a number");
                return null;
            }

            public bool Bool(string key, bool fallback)
            {
                return OptBool(key) ?? fallback;
            }

            public bool? OptBool(string key)
            {
                var t = Raw(key);
                if (t == null) return null;
                if (t.Type == JTokenType.Boolean) return t.Value<bool>();
                Error("'" + key + "' must be true or false");
                return null;
            }

            public string Enum(string key, string fallback, params string[] allowed)
            {
                var value = String(key, fallback);
                if (allowed.Contains(value)) return value;
                Error("'" + key + "' is '" + value + "', expected one of " + string.Join(" | ", allowed));
                return fallback;
            }

            public FloatRange Range(string key, FloatRange fallback)
            {
                var t = Raw(key);
                if (t == null) return fallback;
                try
                {
                    return ToRange(t);
                }
                catch (Exception)
                {
                    Error("'" + key + "' must be a number or [min, max]");
                    return fallback;
                }
            }

            public Vector3 Vector(string key, Vector3 fallback)
            {
                var t = Raw(key);
                if (t == null) return fallback;
                if (t is JArray a && a.Count >= 2 && a.Count <= 3)
                    return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a.Count == 3 ? a[2].Value<float>() : fallback.z);
                Error("'" + key + "' must be [x, y] or [x, y, z]");
                return fallback;
            }

            public JArray Array(string key)
            {
                var t = Raw(key);
                if (t == null) return null;
                if (t is JArray a) return a;
                Error("'" + key + "' must be an array");
                return null;
            }

            public JObject Object(string key)
            {
                var t = Raw(key);
                if (t == null) return null;
                if (t is JObject o) return o;
                Error("'" + key + "' must be an object");
                return null;
            }

            public List<CurveKey> Curve(string key)
            {
                var a = Array(key);
                if (a == null) return null;
                var keys = new List<CurveKey>();
                foreach (var k in a)
                {
                    if (k is JArray p && p.Count == 2)
                        keys.Add(new CurveKey(p[0].Value<float>(), p[1].Value<float>()));
                    else
                        Error("'" + key + "' keys must be [t, value]");
                }

                CheckTimes(key, keys.Select(k => k.Time).ToList());
                return keys;
            }

            public List<ColorKey> ColorCurve(string key)
            {
                var a = Array(key);
                if (a == null) return null;
                var keys = new List<ColorKey>();
                foreach (var k in a)
                {
                    if (k is JArray p && p.Count == 2)
                        keys.Add(new ColorKey(p[0].Value<float>(), ToColor(p[1], key)));
                    else
                        Error("'" + key + "' keys must be [t, \"#rrggbb\"]");
                }

                CheckTimes(key, keys.Select(k => k.Time).ToList());
                if (keys.Count > 8) Error("'" + key + "' has more than 8 keys (Unity gradient limit)");
                return keys;
            }

            public Color ToColor(JToken t, string key)
            {
                var s = t.Type == JTokenType.String ? t.Value<string>() : null;
                if (s != null && ColorUtility.TryParseHtmlString(s, out var c)) return c;
                Error("'" + key + "' has an invalid color: " + t);
                return Color.magenta;
            }

            public void Finish()
            {
                foreach (var p in _o.Properties())
                    if (!_used.Contains(p.Name))
                        Error("unknown key '" + p.Name + "'");
            }

            public static FloatRange ToRange(JToken t)
            {
                if (t is JArray a)
                {
                    if (a.Count != 2) throw new FormatException();
                    return new FloatRange(a[0].Value<float>(), a[1].Value<float>());
                }

                var v = t.Value<float>();
                return new FloatRange(v, v);
            }

            private void CheckTimes(string key, List<float> times)
            {
                for (int i = 0; i < times.Count; i++)
                {
                    if (times[i] < 0f || times[i] > 1f) Error("'" + key + "' times must be within 0..1 (normalized lifetime)");
                    if (i > 0 && times[i] < times[i - 1]) Error("'" + key + "' times must be ascending");
                }
            }
        }

        #endregion
    }
}
#endif
