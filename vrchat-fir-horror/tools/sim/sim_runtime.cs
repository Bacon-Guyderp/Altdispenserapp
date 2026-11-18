// A working stand-in for the parts of Unity, VRChat and UdonSharp the Fir Warning scripts use, so the
// real scripts can be run, unchanged, with several simulated players. Each player is a separate client
// with its own copy of every object; [UdonSynced] fields travel between copies only through
// RequestSerialization (and only from the owner), with network delay, like VRChat.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine {
  public class Object {
    string nm = "";
    public virtual string name { get { return nm; } set { nm = value; } }
    public static implicit operator bool(Object o) { return !ReferenceEquals(o, null); }
    // like Unity: compare by identity (without these, the bool conversion above makes any two objects "equal")
    public static bool operator ==(Object a, Object b) { return ReferenceEquals(a, b); }
    public static bool operator !=(Object a, Object b) { return !ReferenceEquals(a, b); }
    public override bool Equals(object o) { return ReferenceEquals(this, o); }
    public override int GetHashCode() { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this); }
    public override string ToString() { return name; }
    public static void Destroy(Object o) { DestroyImmediate(o); }
    public static void DestroyImmediate(Object o) {
      var g = o as GameObject; if (g != null) { g.destroyed = true; g.activeSelf = false; g.transform.SetParent(null, false); return; }
      var c = o as Component; if (c != null && c.go != null) c.go.comps.Remove(c);
    }
    static Sim.Client Here { get { return Sim.Building ?? Sim.Current; } }
    public static T[] FindObjectsOfType<T>() where T : Object { var r = new List<T>(); if (Here != null) foreach (var g in Here.objects) if (!g.destroyed && g.activeInHierarchy) foreach (var c in g.comps) if (c is T) r.Add(c as T); return r.ToArray(); }
    public static T FindObjectOfType<T>() where T : Object { var a = FindObjectsOfType<T>(); return a.Length > 0 ? a[0] : null; }
  }

  public class Component : Object {
    internal GameObject go;
    public override string name { get { return go != null ? go.name : base.name; } set { if (go != null) go.name = value; else base.name = value; } }
    public GameObject gameObject { get { return go; } }
    public Transform transform { get { return go.transform; } }
    public T GetComponent<T>() where T : class { return go.GetComponent<T>(); }
    public T[] GetComponents<T>() where T : class { var r = new List<T>(); foreach (var c in go.comps) if (c is T) r.Add(c as T); return r.ToArray(); }
    public Component GetComponent(Type t) { foreach (var c in go.comps) if (t.IsInstanceOfType(c)) return c; return null; }
    public T[] GetComponentsInChildren<T>() where T : class { return go.GetComponentsInChildren<T>(false); }
    public T[] GetComponentsInChildren<T>(bool inactive) where T : class { return go.GetComponentsInChildren<T>(inactive); }
    public T GetComponentInChildren<T>(bool inactive) where T : class { var a = go.GetComponentsInChildren<T>(inactive); return a.Length > 0 ? a[0] : null; }
  }
  public class Behaviour : Component { public bool enabled = true; }
  public class MonoBehaviour : Behaviour { }

  public class GameObject : Object {
    public Transform transform;
    public bool activeSelf = true;
    public int layer;
    internal readonly List<Component> comps = new List<Component>();
    internal int netId;
    internal Sim.Client client;
    internal bool destroyed;
    public string primitive;   // set by CreatePrimitive (for the fly-through renderer)
    public GameObject() : this("New Game Object") { }
    public GameObject(string n) {
      name = n; client = Sim.Building; netId = client.nextNetId++; client.objects.Add(this);
      transform = new Transform(); transform.go = this; comps.Add(transform);
    }
    public bool activeInHierarchy { get { return activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy); } }
    public void SetActive(bool b) { activeSelf = b; }
    public T AddComponent<T>() where T : Component { var c = (T)Activator.CreateInstance(typeof(T)); c.go = this; comps.Add(c); return c; }
    public T GetComponent<T>() where T : class { foreach (var c in comps) if (c is T) return c as T; return null; }
    public Component AddComponent(Type t) { var c = (Component)Activator.CreateInstance(t); c.go = this; comps.Add(c); return c; }
    public T[] GetComponentsInChildren<T>(bool inactive) where T : class {
      var r = new List<T>(); Collect(transform, r, inactive); return r.ToArray();
    }
    static void Collect<T>(Transform t, List<T> r, bool inactive) where T : class {
      if (!inactive && !t.gameObject.activeSelf) return;
      foreach (var c in t.gameObject.comps) if (c is T) r.Add(c as T);
      foreach (var k in t.kids) Collect(k, r, inactive);
    }
    public static GameObject Find(string n) { var h = Sim.Building ?? Sim.Current; if (h != null) foreach (var g in h.objects) if (!g.destroyed && g.name == n && g.transform.parent == null) return g; return null; }
    public static GameObject CreatePrimitive(PrimitiveType p) {
      var g = new GameObject(p.ToString()); g.primitive = p.ToString(); g.AddComponent<MeshFilter>(); g.AddComponent<MeshRenderer>();
      if (p == PrimitiveType.Cube) g.AddComponent<BoxCollider>(); else if (p == PrimitiveType.Sphere) g.AddComponent<SphereCollider>();
      else if (p == PrimitiveType.Capsule || p == PrimitiveType.Cylinder) g.AddComponent<CapsuleCollider>(); else g.AddComponent<MeshCollider>();
      return g;
    }
  }

  public class Transform : Component {
    public Vector3 localPosition; public Quaternion localRotation = Quaternion.identity; public Vector3 localScale = Vector3.one;
    Transform par;
    internal readonly List<Transform> kids = new List<Transform>();
    public int childCount { get { return kids.Count; } }
    public Transform GetChild(int i) { return kids[i]; }
    public Transform parent { get { return par; } }
    void Reparent(Transform p) { if (par != null) par.kids.Remove(this); par = p; if (p != null) p.kids.Add(this); }
    public void SetParent(Transform p) { var wp = position; var wr = rotation; Reparent(p); position = wp; rotation = wr; }
    public void SetParent(Transform p, bool keepWorld) { if (keepWorld) SetParent(p); else Reparent(p); }
    public Vector3 lossyScale { get { return par == null ? localScale : Vector3.Scale(par.lossyScale, localScale); } }
    public Vector3 position {
      get { return par == null ? localPosition : par.position + par.rotation * Vector3.Scale(par.lossyScale, localPosition); }
      set { localPosition = par == null ? value : Vector3.Unscale(Quaternion.Inverse(par.rotation) * (value - par.position), par.lossyScale); }
    }
    public bool IsChildOf(Transform p) { var t = this; while (t != null) { if (t == p) return true; t = t.par; } return false; }
    public Transform root { get { var t = this; while (t.par != null) t = t.par; return t; } }
    public Transform Find(string n) { foreach (var k in kids) if (k.gameObject.name == n) return k; return null; }
    public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
    public Quaternion rotation {
      get { return par == null ? localRotation : par.rotation * localRotation; }
      set { localRotation = par == null ? value : Quaternion.Inverse(par.rotation) * value; }
    }
    public Vector3 eulerAngles { get { return rotation.eulerAngles; } set { rotation = Quaternion.Euler(value); } }
    public Vector3 localEulerAngles { get { return localRotation.eulerAngles; } set { localRotation = Quaternion.Euler(value); } }
    public Vector3 forward { get { return rotation * Vector3.forward; } }
    public Vector3 InverseTransformPoint(Vector3 p) { return Vector3.Unscale(Quaternion.Inverse(rotation) * (p - position), lossyScale); }
    public Vector3 TransformPoint(Vector3 p) { return position + rotation * Vector3.Scale(lossyScale, p); }
  }

  public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } } }

  public struct Vector3 {
    public float x, y, z;
    public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
    public static Vector3 zero { get { return new Vector3(0, 0, 0); } } public static Vector3 one { get { return new Vector3(1, 1, 1); } }
    public static Vector3 up { get { return new Vector3(0, 1, 0); } } public static Vector3 down { get { return new Vector3(0, -1, 0); } }
    public static Vector3 forward { get { return new Vector3(0, 0, 1); } } public static Vector3 back { get { return new Vector3(0, 0, -1); } }
    public static Vector3 right { get { return new Vector3(1, 0, 0); } } public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
    public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
    public float sqrMagnitude { get { return x * x + y * y + z * z; } }
    public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
    public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
    public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
    public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
    public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x * b, a.y * b, a.z * b); }
    public static Vector3 operator *(float b, Vector3 a) { return a * b; }
    public static Vector3 operator /(Vector3 a, float b) { return new Vector3(a.x / b, a.y / b, a.z / b); }
    public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 1e-10f; }
    public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
    public override bool Equals(object o) { return o is Vector3 && this == (Vector3)o; } public override int GetHashCode() { return 0; }
    public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
    public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
    public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) { var v = b - a; float m = v.magnitude; return m <= d || m < 1e-6f ? b : a + v / m * d; }
    public override string ToString() { return string.Format("({0:F1}, {1:F1}, {2:F1})", x, y, z); }
    public string ToString(string f) { return ToString(); }
    public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
    internal static Vector3 Unscale(Vector3 a, Vector3 s) { return new Vector3(s.x != 0 ? a.x / s.x : 0, s.y != 0 ? a.y / s.y : 0, s.z != 0 ? a.z / s.z : 0); }
    public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z)); }
    public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z)); }
  }

  public struct Quaternion {
    public float x, y, z, w;
    public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
    static Quaternion Axis(Vector3 a, float deg) { float r = deg * Mathf.Deg2Rad * 0.5f; float s = (float)Math.Sin(r); return new Quaternion(a.x * s, a.y * s, a.z * s, (float)Math.Cos(r)); }
    public static Quaternion AngleAxis(float deg, Vector3 axis) { return Axis(axis.normalized, deg); }
    public static Quaternion Euler(float x, float y, float z) { return Axis(Vector3.up, y) * Axis(Vector3.right, x) * Axis(Vector3.forward, z); }
    public static Quaternion Euler(Vector3 v) { return Euler(v.x, v.y, v.z); }
    public static Quaternion operator *(Quaternion a, Quaternion b) {
      return new Quaternion(a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
    }
    public static Vector3 operator *(Quaternion q, Vector3 v) {
      var u = new Vector3(q.x, q.y, q.z); var t = Vector3.Cross(u, v) * 2f; return v + t * q.w + Vector3.Cross(u, t);
    }
    public static Quaternion Inverse(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, q.w); }
    public static float Dot(Quaternion a, Quaternion b) { return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w; }
    public static float Angle(Quaternion a, Quaternion b) { float d = Math.Min(Math.Abs(Dot(a, b)), 1f); return (float)(2.0 * Math.Acos(d)) * Mathf.Rad2Deg; }
    public static Quaternion Slerp(Quaternion a, Quaternion b, float t) {
      t = Mathf.Clamp01(t); float d = Dot(a, b); if (d < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
      var r = new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
      float m = (float)Math.Sqrt(Dot(r, r)); return new Quaternion(r.x / m, r.y / m, r.z / m, r.w / m);
    }
    public static Quaternion RotateTowards(Quaternion a, Quaternion b, float maxDeg) { float ang = Angle(a, b); return ang < 1e-3f ? b : Slerp(a, b, Math.Min(1f, maxDeg / ang)); }
    public static Quaternion LookRotation(Vector3 f) { return LookRotation(f, Vector3.up); }
    public static Quaternion LookRotation(Vector3 f, Vector3 up) {
      f = f.normalized; if (f.sqrMagnitude < 1e-8f) return identity;
      float yaw = (float)Math.Atan2(f.x, f.z) * Mathf.Rad2Deg; float pitch = -(float)Math.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
      return Euler(pitch, yaw, 0);
    }
    public static Quaternion FromToRotation(Vector3 a, Vector3 b) { var ax = Vector3.Cross(a, b); float ang = (float)Math.Acos(Mathf.Clamp(Vector3.Dot(a.normalized, b.normalized), -1, 1)) * Mathf.Rad2Deg; return ax.sqrMagnitude < 1e-8f ? identity : AngleAxis(ang, ax); }
    public Vector3 eulerAngles { get { var f = this * Vector3.forward; float yaw = (float)Math.Atan2(f.x, f.z) * Mathf.Rad2Deg; if (yaw < 0) yaw += 360; float pitch = -(float)Math.Asin(Mathf.Clamp(f.y, -1, 1)) * Mathf.Rad2Deg; return new Vector3(pitch, yaw, 0); } }
  }

  public struct Color { public float r, g, b, a; public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; } public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white { get { return new Color(1, 1, 1); } } public static Color black { get { return new Color(0, 0, 0); } } public static Color red { get { return new Color(1, 0, 0); } } public static Color clear { get { return new Color(0, 0, 0, 0); } } public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a * f); } public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); } public static Color Lerp(Color a, Color b, float t) { return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); } }

  public static class Mathf {
    public const float PI = 3.14159265f, Rad2Deg = 57.2957795f, Deg2Rad = 0.0174532925f;
    public static float Sin(float f) { return (float)Math.Sin(f); } public static float Cos(float f) { return (float)Math.Cos(f); }
    public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); } public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
    public static float Abs(float f) { return Math.Abs(f); } public static int Abs(int f) { return Math.Abs(f); }
    public static float Max(float a, float b) { return Math.Max(a, b); } public static float Min(float a, float b) { return Math.Min(a, b); }
    public static int Max(int a, int b) { return Math.Max(a, b); } public static int Min(int a, int b) { return Math.Min(a, b); }
    public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; } public static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
    public static float Clamp01(float v) { return Clamp(v, 0, 1); } public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
    public static float Repeat(float t, float l) { return t - (float)Math.Floor(t / l) * l; } public static float Floor(float f) { return (float)Math.Floor(f); }
    public static int FloorToInt(float f) { return (int)Math.Floor(f); } public static int RoundToInt(float f) { return (int)Math.Round(f); }
    public static float MoveTowards(float a, float b, float d) { return Math.Abs(b - a) <= d ? b : a + Math.Sign(b - a) * d; }
    public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = t * t * (3 - 2 * t); return a + (b - a) * t; }
    public static float PingPong(float t, float l) { t = Repeat(t, l * 2); return l - Math.Abs(t - l); }
    public static float Sign(float f) { return f >= 0 ? 1 : -1; }
  }
  public static class Time { public static float time, deltaTime = 0.05f; public static int frameCount; }
  public static class Random {
    public static System.Random rng = new System.Random(1234);
    public static float value { get { return (float)rng.NextDouble(); } }
    public static float Range(float a, float b) { return a + (b - a) * value; }
    public static int Range(int a, int b) { return b <= a ? a : rng.Next(a, b); }
    public static Vector3 insideUnitSphere { get { return new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)) * 0.57f; } }
  }
  public class AudioClip : Object { public float length = 1.5f; }
  public class AudioSource : Behaviour {
    public AudioClip clip; public bool loop, playOnAwake; public float volume = 1, pitch = 1, spatialBlend, minDistance, maxDistance, dopplerLevel; public AudioRolloffMode rolloffMode;
    float until;
    public bool isPlaying { get { return Time.time < until; } }
    public void Play() { until = Time.time + (clip != null ? clip.length : 1f); Sim.Log("sound", go.name + (clip != null ? " " + clip.name : "")); }
    public void Stop() { until = 0; } public void Pause() { } public void UnPause() { }
    public void PlayOneShot(AudioClip c) { if (c != null) Sim.Log("sound", c.name); } public void PlayOneShot(AudioClip c, float v) { PlayOneShot(c); }
  }
  public enum AudioRolloffMode { Logarithmic, Linear, Custom }
  public struct Bounds { public Vector3 center, size; public Vector3 extents { get { return size * 0.5f; } } public Vector3 min { get { return center - extents; } } public Vector3 max { get { return center + extents; } } }
  public class Collider : Component {
    public bool isTrigger; public bool enabled = true;
    internal virtual Vector3 LocalCenter { get { return Vector3.zero; } }
    internal virtual Vector3 LocalSize { get { return Vector3.one; } }
    /// world-space axis-aligned bounds (rotated boxes are enclosed)
    public Bounds bounds { get {
      var t = transform; var c = t.TransformPoint(LocalCenter); var h = Vector3.Scale(t.lossyScale, LocalSize) * 0.5f;
      var r = t.rotation; var ex = Vector3.zero;
      foreach (var ax in new[] { new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), new Vector3(0, 0, h.z) }) { var w = r * ax; ex = ex + new Vector3(Math.Abs(w.x), Math.Abs(w.y), Math.Abs(w.z)); }
      return new Bounds { center = c, size = ex * 2 };
    } }
  }
  public class BoxCollider : Collider { public Vector3 center; public Vector3 size = Vector3.one; internal override Vector3 LocalCenter { get { return center; } } internal override Vector3 LocalSize { get { return size; } } }
  public class SphereCollider : Collider { public Vector3 center; public float radius = 0.5f; internal override Vector3 LocalCenter { get { return center; } } internal override Vector3 LocalSize { get { return Vector3.one * radius * 2; } } }
  public class CapsuleCollider : Collider { public Vector3 center; public float radius = 0.5f, height = 2; internal override Vector3 LocalSize { get { return new Vector3(radius * 2, height, radius * 2); } } }
  public class MeshCollider : Collider { internal override Vector3 LocalSize { get { return new Vector3(1, 0.01f, 1); } } }
  public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
  public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
  public class Rigidbody : Component { public Vector3 velocity; public float mass = 1; public bool useGravity = true, isKinematic; public RigidbodyInterpolation interpolation; public CollisionDetectionMode collisionDetectionMode; }
  public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
  public struct RaycastHit { public Vector3 point; public float distance; public Collider collider; }
  public static class Physics {
    // walls/shelves as axis-aligned boxes (centre, half size)
    public static readonly List<KeyValuePair<Vector3, Vector3>> Boxes = new List<KeyValuePair<Vector3, Vector3>>();
    static readonly Dictionary<Sim.Client, List<Collider>> cache = new Dictionary<Sim.Client, List<Collider>>();
    static readonly Dictionary<Collider, Bounds> boundsCache = new Dictionary<Collider, Bounds>();
    public static void SyncTransforms() { cache.Clear(); boundsCache.Clear(); }
    static List<Collider> All() {
      var here = Sim.Building ?? Sim.Current; if (here == null) return new List<Collider>();
      List<Collider> l; if (cache.TryGetValue(here, out l)) return l;
      l = new List<Collider>(); foreach (var g in here.objects) if (!g.destroyed) foreach (var c in g.comps) if (c is Collider) l.Add((Collider)c);
      cache[here] = l; return l;
    }
    static bool Usable(Collider c, int mask, QueryTriggerInteraction q) {
      if (!c.enabled || c.go == null || c.go.destroyed || !c.go.activeInHierarchy) return false;
      if (!c.go.comps.Contains(c)) return false;
      if (c.isTrigger && q == QueryTriggerInteraction.Ignore) return false;
      return ((mask >> c.go.layer) & 1) == 1;
    }
    static Bounds B(Collider c) { Bounds b; if (c.GetComponent<Rigidbody>() == null && boundsCache.TryGetValue(c, out b)) return b; b = c.bounds; boundsCache[c] = b; return b; }
    public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float max, int mask, QueryTriggerInteraction q) {
      h = default(RaycastHit); float best = max; bool hit = false;
      foreach (var b in Boxes) { float t; if (Slab(o, d, b.Key - b.Value, b.Key + b.Value, out t) && t <= best) { best = t; hit = true; } }
      foreach (var c in All()) {
        if (!Usable(c, mask, q)) continue; var bb = B(c); float t;
        if (Slab(o, d, bb.min, bb.max, out t) && t <= best) { best = t; hit = true; h.collider = c; }
      }
      if (hit) { h.distance = best; h.point = o + d * best; }
      return hit;
    }
    public static bool CheckSphere(Vector3 p, float r, int mask, QueryTriggerInteraction q) {
      foreach (var c in All()) {
        if (!Usable(c, mask, q)) continue; var bb = B(c);
        var cl = Vector3.Max(bb.min, Vector3.Min(bb.max, p)); if ((cl - p).sqrMagnitude <= r * r) return true;
      }
      return false;
    }
    public static bool Raycast(Vector3 o, Vector3 d, float max, int mask, QueryTriggerInteraction q) { RaycastHit h; return Raycast(o, d, out h, max, mask, q); }
    public static bool Linecast(Vector3 a, Vector3 b, int mask, QueryTriggerInteraction q) { var d = b - a; return Raycast(a, d.normalized, d.magnitude, mask, q); }
    static bool Slab(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx, out float t) {
      float t0 = 0, t1 = 1e9f; t = 0;
      float[] O = { o.x, o.y, o.z }, D = { d.x, d.y, d.z }, A = { mn.x, mn.y, mn.z }, B = { mx.x, mx.y, mx.z };
      for (int i = 0; i < 3; i++) {
        if (Math.Abs(D[i]) < 1e-8f) { if (O[i] < A[i] || O[i] > B[i]) return false; continue; }
        float a = (A[i] - O[i]) / D[i], b = (B[i] - O[i]) / D[i]; if (a > b) { var s = a; a = b; b = s; }
        t0 = Math.Max(t0, a); t1 = Math.Min(t1, b); if (t0 > t1) return false;
      }
      t = t0; return true;
    }
  }
  public static class Debug { public static readonly List<string> Warnings = new List<string>(); public static void Log(object o) { Sim.BuildLog.Add(o.ToString()); } public static void LogWarning(object o) { Warnings.Add(o.ToString()); } public static void LogWarning(object o, Object c) { Warnings.Add(o.ToString()); } public static void LogError(object o, Object c) { LogError(o); } public static void LogError(object o) { Sim.Fail("Debug.LogError: " + o); } }
  public class Material : Object {
    public Material() { } public Material(Shader s) { shader = s; } public Material(Material m) { shader = m.shader; }
    public Shader shader; public Texture mainTexture; public bool enableInstancing;
    public bool HasProperty(string n) { return true; } public void CopyPropertiesFromMaterial(Material m) { }
    public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
    public readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
    public void SetFloat(string n, float f) { floats[n] = f; } public void SetColor(string n, Color c) { colors[n] = c; }
    public float GetFloat(string n) { float f; return floats.TryGetValue(n, out f) ? f : 0; }
  }
  public class Renderer : Component { public Material sharedMaterial; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; public UnityEngine.Rendering.LightProbeUsage lightProbeUsage; public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage; }
}

namespace TMPro {
  public class TextMeshPro : UnityEngine.Renderer {
    public string text = ""; public UnityEngine.Color color; public UnityEngine.Material fontSharedMaterial; public TextAlignmentOptions alignment;
    public bool enableWordWrapping, enableAutoSizing, richText; public float fontSize, fontSizeMin, fontSizeMax; public FontStyles fontStyle;
    UnityEngine.RectTransform rt; public UnityEngine.RectTransform rectTransform { get { if (rt == null) rt = new UnityEngine.RectTransform(); return rt; } }
  }
}

namespace VRC.SDKBase {
  using UnityEngine;
  public class VRCPlayerApi {
    public int playerId; public string displayName;
    public Vector3 pos; public Quaternion rot = Quaternion.identity; public float pitch;
    public bool isLocal { get { return Sim.Current != null && Sim.Current.player == this; } }
    public bool isMaster { get { return Sim.Master == this; } }
    public Vector3 GetPosition() { return pos; }
    public Quaternion GetRotation() { return rot; }
    public void TeleportTo(Vector3 p, Quaternion r) {
      if (!isLocal) Sim.Fail("TeleportTo called on a remote player (VRChat ignores this)");
      pos = p; rot = r; Sim.Log("teleport", displayName + " -> " + p);
    }
    public struct TrackingData { public Vector3 position; public Quaternion rotation; }
    public enum TrackingDataType { Head, LeftHand, RightHand, Origin }
    public TrackingData GetTrackingData(TrackingDataType t) { var d = new TrackingData(); d.position = pos + new Vector3(0, 1.6f, 0); d.rotation = rot * Quaternion.Euler(pitch, 0, 0); return d; }
    public static int GetPlayerCount() { return Sim.Players.Count; }
    public static VRCPlayerApi[] GetPlayers(VRCPlayerApi[] arr) { for (int i = 0; i < Sim.Players.Count && i < arr.Length; i++) arr[i] = Sim.Players[i]; return arr; }
    public void LookAt(Vector3 p) { var d = p - (pos + new Vector3(0, 1.6f, 0)); rot = Quaternion.Euler(0, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0); pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg; }
  }
  public static class Networking {
    public static VRCPlayerApi LocalPlayer { get { return Sim.Current.player; } }
    public static bool IsMaster { get { return Sim.Master == Sim.Current.player; } }
    public static bool IsOwner(GameObject g) { return Sim.OwnerOf(g) == Sim.Current.player; }
    public static VRCPlayerApi GetOwner(GameObject g) { return Sim.OwnerOf(g); }
    public static void SetOwner(VRCPlayerApi p, GameObject g) { Sim.SetOwner(p, g); }
    public static int GetServerTimeInMilliseconds() { return 50000000 + (int)(Time.time * 1000); }
  }
  public class VRC_SceneDescriptor : UnityEngine.MonoBehaviour { public UnityEngine.Transform[] spawns; public float RespawnHeightY; public UnityEngine.GameObject ReferenceCamera; }
  public static class Utilities { public static bool IsValid(object o) { return o != null; } }
  public class VRC_Pickup : MonoBehaviour { public string InteractionText, UseText; public float proximity, ThrowVelocityBoostScale; public AutoHoldMode AutoHold; public enum AutoHoldMode { AutoDetect, Yes, No } public bool IsHeld; public void Drop() { IsHeld = false; Sim.Log("pickup", go.name + " dropped"); } }
}
namespace VRC.SDK3.Components {
  public class VRCPickup : VRC.SDKBase.VRC_Pickup { }
  public class VRCSceneDescriptor : VRC.SDKBase.VRC_SceneDescriptor { }
  public class VRCObjectSync : UnityEngine.MonoBehaviour {
    internal UnityEngine.Vector3 home; internal bool homeSet;
    public void Respawn() { if (!VRC.SDKBase.Networking.IsOwner(gameObject)) Sim.Fail("ObjectSync.Respawn by a non-owner"); transform.position = home; }
  }
}

namespace VRC.Udon {
  /// The real Udon component. Calls are checked strictly: a missing variable or event, or a value of the
  /// wrong type, is reported (in VRChat these fail silently or stop the script).
  public class UdonBehaviour : UnityEngine.MonoBehaviour {
    public string interactText; public float proximity;
    internal UdonSharp.UdonSharpBehaviour program;
    // what the builder's checks look at: the program, and the stored public variables (read from the proxy)
    public object programSource { get { return program != null ? program.GetType().Name : null; } }
    public VarTable publicVariables { get { return new VarTable { u = this }; } }
    public class VarTable {
      internal UdonBehaviour u;
      public bool TryGetVariableValue(string n, out object v) {
        v = null; if (u.program == null) return false;
        var f = Field(u.program, n); if (f == null) return false;
        v = f.GetValue(u.program); return true;
      }
    }
    UdonSharp.UdonSharpBehaviour P { get { if (program == null) Sim.Fail("UdonBehaviour on '" + go.name + "' has no program"); return program; } }
    static System.Reflection.FieldInfo Field(object p, string n) { return p.GetType().GetField(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); }
    public void SendCustomEvent(string e) {
      var p = P; if (p == null) return;
      if (Sim.Halted(p)) { Sim.Log("udon", "event " + e + " ignored: " + p.GetType().Name + " is halted"); return; }
      Sim.Invoke(p, e);
    }
    public void SetProgramVariable(string n, object v) {
      var p = P; if (p == null) return; var f = Field(p, n);
      if (f == null) { Sim.Fail("SetProgramVariable: " + p.GetType().Name + " has no variable '" + n + "'"); return; }
      if (v != null && !f.FieldType.IsInstanceOfType(v)) { Sim.Fail("SetProgramVariable: " + p.GetType().Name + "." + n + " is " + f.FieldType.Name + ", got " + v.GetType().Name); return; }
      f.SetValue(p, v);
    }
    public object GetProgramVariable(string n) {
      var p = P; if (p == null) return null; var f = Field(p, n);
      if (f == null) { Sim.Fail("GetProgramVariable: " + p.GetType().Name + " has no variable '" + n + "'"); return null; }
      return f.GetValue(p);
    }
  }
}

namespace UdonSharp {
  public enum BehaviourSyncMode { Any, None, Continuous, Manual, NoVariableSync }
  [AttributeUsage(AttributeTargets.Class)] public class UdonBehaviourSyncModeAttribute : Attribute { public BehaviourSyncMode mode; public UdonBehaviourSyncModeAttribute(BehaviourSyncMode m) { mode = m; } }
  [AttributeUsage(AttributeTargets.Field)] public class UdonSyncedAttribute : Attribute { }
  public class UdonSharpBehaviour : UnityEngine.MonoBehaviour {
    internal bool started;
    public void SendCustomEvent(string e) { Sim.Invoke(this, e); }
    public void SendCustomEventDelayedSeconds(string e, float s) { Sim.Delay(this, e, s); }
    public void RequestSerialization() { Sim.RequestSerialization(this); }
    public virtual void Interact() { }
    public virtual void OnDeserialization() { }
    public virtual void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi p) { }
    public virtual void OnPickup() { } public virtual void OnDrop() { } public virtual void OnPickupUseDown() { }
  }
}

// ======================================================================= the simulator
public static class Sim {
  public static readonly List<string> BuildLog = new List<string>();
  public class Client {
    public VRC.SDKBase.VRCPlayerApi player;
    public readonly List<UnityEngine.GameObject> objects = new List<UnityEngine.GameObject>();
    public int nextNetId;
    public object scene;   // whatever the scene builder returns
  }
  public static readonly List<VRC.SDKBase.VRCPlayerApi> Players = new List<VRC.SDKBase.VRCPlayerApi>();
  public static readonly List<Client> Clients = new List<Client>();
  public static Client Current, Building;
  public static VRC.SDKBase.VRCPlayerApi Master { get { return Players.Count > 0 ? Players[0] : null; } }
  static readonly Dictionary<int, VRC.SDKBase.VRCPlayerApi> owners = new Dictionary<int, VRC.SDKBase.VRCPlayerApi>();
  public static float Latency = 0.15f;
  public static readonly List<string> Failures = new List<string>();
  public static readonly List<string> Events = new List<string>();
  public static bool Verbose;
  public static Action OnStep;

  public static void Fail(string m) { string s = "[" + (Current != null ? Current.player.displayName : "-") + " t=" + UnityEngine.Time.time.ToString("F2") + "] " + m; Failures.Add(s); Console.WriteLine("    !! " + s); }
  public static void Log(string kind, string m) { string s = (Current != null ? Current.player.displayName : "-") + " " + kind + ": " + m; Events.Add(s); if (Verbose) Console.WriteLine("      " + s); }

  public static VRC.SDKBase.VRCPlayerApi OwnerOf(UnityEngine.GameObject g) { VRC.SDKBase.VRCPlayerApi p; return owners.TryGetValue(g.netId, out p) && Players.Contains(p) ? p : Master; }
  public static void SetOwner(VRC.SDKBase.VRCPlayerApi p, UnityEngine.GameObject g) {
    var old = OwnerOf(g); owners[g.netId] = p;
    if (old != p) foreach (var c in Clients) With(c, () => { foreach (var b in Behaviours(c.objects[g.netId])) b.OnOwnershipTransferred(p); });
  }

  // ---- events
  class Pending { public float at; public Client c; public UdonSharp.UdonSharpBehaviour b; public string ev; public Dictionary<string, object> data; public int netId; public Type type; public bool lateJoin; }
  static readonly List<Pending> queue = new List<Pending>();

  public static void Invoke(UdonSharp.UdonSharpBehaviour b, string e) {
    var m = b.GetType().GetMethod(e, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
    if (m == null) { Fail("SendCustomEvent: " + b.GetType().Name + " has no public method '" + e + "' (Udon would do nothing)"); return; }
    try { m.Invoke(b, null); } catch (TargetInvocationException ex) { Crash(b, e, ex.InnerException); }
  }
  public static void Delay(UdonSharp.UdonSharpBehaviour b, string e, float s) { queue.Add(new Pending { at = UnityEngine.Time.time + s, c = Current, b = b, ev = e }); }

  public static void RequestSerialization(UdonSharp.UdonSharpBehaviour b) {
    var attr = (UdonSharp.UdonBehaviourSyncModeAttribute)Attribute.GetCustomAttribute(b.GetType(), typeof(UdonSharp.UdonBehaviourSyncModeAttribute));
    if (attr == null || attr.mode != UdonSharp.BehaviourSyncMode.Manual) { Fail("RequestSerialization on " + b.GetType().Name + " which isn't Manual sync"); return; }
    if (!VRC.SDKBase.Networking.IsOwner(b.gameObject)) { Log("sync", "ignored RequestSerialization from non-owner on " + b.GetType().Name); return; }
    var data = new Dictionary<string, object>();
    foreach (var f in SyncedFields(b.GetType())) data[f.Name] = f.GetValue(b);
    foreach (var c in Clients) if (c != Current)
      queue.Add(new Pending { at = UnityEngine.Time.time + Latency, c = c, data = data, netId = b.gameObject.netId, type = b.GetType() });
  }
  static IEnumerable<FieldInfo> SyncedFields(Type t) {
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
      if (Attribute.IsDefined(f, typeof(UdonSharp.UdonSyncedAttribute))) yield return f;
  }
  static void Deliver(Client c, int netId, Type t, Dictionary<string, object> data) {
    if (!Clients.Contains(c)) return;
    var go = c.objects[netId];
    foreach (var b in Behaviours(go)) if (b.GetType() == t) {
      foreach (var f in SyncedFields(t)) f.SetValue(b, data[f.Name]);
      With(c, () => { try { b.OnDeserialization(); } catch (Exception ex) { Crash(b, "OnDeserialization", ex); } });
    }
  }

  static void Crash(UdonSharp.UdonSharpBehaviour b, string where, Exception ex) {
    Fail("CRASH in " + b.GetType().Name + "." + where + ": " + ex.GetType().Name + " " + ex.Message + "\n" + ex.StackTrace);
    // Udon halts a behaviour that throws; mark it so it gets no more updates
    halted.Add(b);
  }
  static readonly HashSet<UdonSharp.UdonSharpBehaviour> halted = new HashSet<UdonSharp.UdonSharpBehaviour>();
  public static bool Halted(UdonSharp.UdonSharpBehaviour b) { return halted.Contains(b); }

  public static IEnumerable<UdonSharp.UdonSharpBehaviour> Behaviours(UnityEngine.GameObject g) { foreach (var c in g.comps) { var b = c as UdonSharp.UdonSharpBehaviour; if (b != null) yield return b; } }
  /// The UdonBehaviour behind a U# behaviour (made on demand, like U#'s editor setup does).
  public static VRC.Udon.UdonBehaviour UB(UdonSharp.UdonSharpBehaviour b) {
    if (b == null) return null;
    foreach (var c in b.gameObject.comps) { var u = c as VRC.Udon.UdonBehaviour; if (u != null && u.program == b) return u; }
    var nu = b.gameObject.AddComponent<VRC.Udon.UdonBehaviour>(); nu.program = b; return nu;
  }
  public static void With(Client c, Action a) { var old = Current; Current = c; try { a(); } finally { Current = old; } }

  // ---- players
  public static Func<Client, object> SceneBuilder;
  public static Client Join(string name, UnityEngine.Vector3 spawn) {
    var p = new VRC.SDKBase.VRCPlayerApi { playerId = Players.Count + 1, displayName = name, pos = spawn };
    Players.Add(p);
    var c = new Client { player = p };
    Building = c; Current = c;
    c.scene = SceneBuilder(c);
    InitSerializedDefaults(c);
    Building = null; Current = null;
    Clients.Add(c);
    // late joiner: VRChat sends the current state of every synced behaviour (read from its owner when it arrives)
    if (Clients.Count > 1) queue.Add(new Pending { at = UnityEngine.Time.time + Latency * 2, c = c, lateJoin = true });
    return c;
  }
  static void SendFullState(Client c) {
    var src = Clients[0] == c ? Clients[1] : Clients[0];
    foreach (var go in src.objects) foreach (var b in Behaviours(go)) {
      var attr = (UdonSharp.UdonBehaviourSyncModeAttribute)Attribute.GetCustomAttribute(b.GetType(), typeof(UdonSharp.UdonBehaviourSyncModeAttribute));
      if (attr == null || attr.mode != UdonSharp.BehaviourSyncMode.Manual) continue;
      var owner = OwnerOf(go); var oc = Clients.Find(x => x.player == owner && x != c) ?? src;
      var ob = Behaviours(oc.objects[go.netId]).First(x => x.GetType() == b.GetType());
      var data = new Dictionary<string, object>(); foreach (var f in SyncedFields(b.GetType())) data[f.Name] = f.GetValue(ob);
      Deliver(c, go.netId, b.GetType(), data);
    }
  }
  static T First<T>(this IEnumerable<T> e, Func<T, bool> f) { foreach (var x in e) if (f(x)) return x; return default(T); }
  public static void Leave(Client c) { Clients.Remove(c); Players.Remove(c.player); }

  // Unity serializes empty arrays and empty strings for unassigned public fields
  static void InitSerializedDefaults(Client c) {
    foreach (var go in c.objects) foreach (var b in Behaviours(go))
      foreach (var f in b.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
        if (f.GetValue(b) != null) continue;
        if (f.FieldType.IsArray) f.SetValue(b, Array.CreateInstance(f.FieldType.GetElementType(), 0));
        else if (f.FieldType == typeof(string)) f.SetValue(b, "");
      }
  }

  // ---- the main loop
  public static void Step() {
    float t = UnityEngine.Time.time;
    for (int i = 0; i < queue.Count; i++) {
      var p = queue[i]; if (p.at > t) continue; queue.RemoveAt(i); i--;
      if (p.lateJoin) SendFullState(p.c);
      else if (p.data != null) Deliver(p.c, p.netId, p.type, p.data);
      else if (Clients.Contains(p.c)) With(p.c, () => Invoke(p.b, p.ev));
    }
    foreach (var c in Clients.ToArray()) With(c, () => {
      foreach (var go in c.objects.ToArray()) {
        if (!go.activeInHierarchy) continue;
        foreach (var b in Behaviours(go)) {
          if (halted.Contains(b) || !b.enabled) continue;
          if (!b.started) { b.started = true; CallPrivate(b, "Start"); }
          if (halted.Contains(b)) continue;
          CallPrivate(b, "Update");
        }
      }
    });
    // object sync: everyone follows the owner's copy
    if (Clients.Count > 1) foreach (var go in Clients[0].objects) {
      if (go.GetComponent<VRC.SDK3.Components.VRCObjectSync>() == null) continue;
      var oc = Clients.Find(x => x.player == OwnerOf(go)); if (oc == null) continue;
      var src = oc.objects[go.netId];
      foreach (var c in Clients) if (c != oc) { c.objects[go.netId].transform.position = src.transform.position; }
    }
    if (OnStep != null) OnStep();
    UnityEngine.Time.time += UnityEngine.Time.deltaTime; UnityEngine.Time.frameCount++;
  }
  static void CallPrivate(UdonSharp.UdonSharpBehaviour b, string name) {
    var m = b.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
    if (m == null) return;
    try { m.Invoke(b, null); } catch (TargetInvocationException ex) { Crash(b, name, ex.InnerException); }
  }
  public static void Run(float seconds) { int n = (int)Math.Round(seconds / UnityEngine.Time.deltaTime); for (int i = 0; i < n; i++) Step(); }
  public static void Press(Client c, UdonSharp.UdonSharpBehaviour b) {
    if (!b.gameObject.activeInHierarchy) { Fail("pressed " + b.gameObject.name + " but it is hidden"); return; }
    With(c, () => { try { b.Interact(); } catch (Exception ex) { Crash(b, "Interact", ex); } });
  }
}
