using System.Collections.Generic;
using UnityEngine;

namespace Lockdown {

/// <summary>
/// Spawns one orb per lost direction, max 4.
///
/// Placement is computed from the PLAYER'S CURRENT POSITION and WHICH DIRECTIONS THEY CAN
/// STILL MOVE IN.
///
/// This is the whole ballgame. Movement is axis-locked and there is no screen wrap, so a lost
/// direction permanently bounds where the player can ever go again: lose Left and your x can
/// never decrease, so every point to your left is unreachable for the rest of the run. An orb
/// placed there is not a challenge, it is a dead pickup that quietly becomes a permanent
/// direction loss when its timer runs out.
///
/// So the spawn region is the rectangle the remaining directions can still reach, and within
/// that region the orb is biased FAR from the player so it still costs real travel time. The
/// difficulty comes from the distance and from the turrets on the way, never from placing it
/// somewhere the player physically cannot go.
/// </summary>
public class OrbSpawner : MonoBehaviour {
    public static OrbSpawner I { get; private set; }

    public Orb orbPrefab;

    [Header("Placement")]
    [Tooltip("Half-extents of the spawnable area. Inset from the 10x6 arena walls so an orb " +
             "never overlaps one.")]
    public Vector2 arenaHalfExtents = new(9.4f, 5.4f);
    public float minDistanceFromPlayer = 3f;
    public float minDistanceFromEnemy  = 3f;
    [Tooltip("Clearance required from walls and pillars.")]
    public float orbRadius = 0.45f;

    [Header("Reachability")]
    [Tooltip("Random points to try per spawn. Higher is more reliable, cost is trivial.")]
    public int samples = 128;
    [Tooltip("Slack on the reachable bound. The player can still collect an orb that is " +
             "roughly this far past the limit, because both have ~0.4 unit radii.")]
    public float pickupTolerance = 0.6f;
    [Tooltip("Fraction of reachable candidates to consider, farthest first. 0.5 means the " +
             "orb lands in the farther half of where you can still get to.")]
    [Range(0.05f, 1f)] public float farthestFraction = 0.5f;

    public static readonly Color[] OrbColors = {
        new(0.20f, 0.53f, 1.00f),   // Up    #3388FF
        new(1.00f, 0.20f, 0.20f),   // Down  #FF3333
        new(1.00f, 0.87f, 0.20f),   // Left  #FFDD33
        new(0.20f, 1.00f, 0.53f),   // Right #33FF88
    };

    readonly Dictionary<Direction, Orb> _live = new();

    void Awake() { I = this; }
    void OnDestroy() { if (I == this) I = null; }

    void OnEnable()  { if (DirectionSystem.I != null) DirectionSystem.I.OnMercyAward += Retire; }
    void OnDisable() { if (DirectionSystem.I != null) DirectionSystem.I.OnMercyAward -= Retire; }

    void Retire(Direction d) {
        if (_live.TryGetValue(d, out var orb) && orb != null) orb.RetireSilently();
        _live.Remove(d);
    }

    public void SpawnFor(Direction d) {
        if (orbPrefab == null || d == Direction.None) return;
        if (_live.TryGetValue(d, out var existing) && existing != null) return;   // one per direction

        if (!TryPickSpawnPoint(d, out Vector2 at)) return;

        Orb orb = Instantiate(orbPrefab, at, Quaternion.identity);
        orb.Init(d, OrbColors[(int)d]);
        _live[d] = orb;
    }

    /// <summary>
    /// The rectangle the player can still reach, given which directions they have left.
    ///
    /// Movement is axis-locked with no wrap, so each lost direction clamps one side of the
    /// arena to the player's current coordinate. Lose Left and x can never go below where you
    /// are standing now; lose Up and y can never go above it.
    /// </summary>
    public static Rect ReachableArea(Vector2 p, Vector2 halfExtents, System.Func<Direction, bool> isActive,
                                     float tolerance) {
        float xMin = -halfExtents.x, xMax = halfExtents.x;
        float yMin = -halfExtents.y, yMax = halfExtents.y;

        if (!isActive(Direction.Left))  xMin = Mathf.Max(xMin, p.x - tolerance);
        if (!isActive(Direction.Right)) xMax = Mathf.Min(xMax, p.x + tolerance);
        if (!isActive(Direction.Down))  yMin = Mathf.Max(yMin, p.y - tolerance);
        if (!isActive(Direction.Up))    yMax = Mathf.Min(yMax, p.y + tolerance);

        // Clamp rather than allow an inverted rect if the player is outside the nominal bounds.
        if (xMax < xMin) xMax = xMin;
        if (yMax < yMin) yMax = yMin;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    /// <summary>
    /// Samples the REACHABLE rectangle for a free point, preferring ones far from the player.
    ///
    /// Three tiers, so it degrades instead of failing:
    ///   1. reachable, free, and at least minDistanceFromPlayer away -> pick among the farthest
    ///   2. reachable and free but all of it is close -> take the farthest one anyway
    ///   3. nothing reachable and free -> do not spawn, rather than drop an orb in a wall
    ///      or somewhere the player can never walk to
    /// </summary>
    bool TryPickSpawnPoint(Direction lost, out Vector2 result) {
        var player = Object.FindFirstObjectByType<PlayerController>();
        Vector2 p = player != null ? (Vector2)player.transform.position : Vector2.zero;

        var ds = DirectionSystem.I;
        Rect area = ReachableArea(p, arenaHalfExtents,
                                  d => ds == null || ds.IsActive(d), pickupTolerance);

        // Gathered ONCE, not per sample - FindObjectsByType in a 128-iteration loop is brutal.
        var turrets = Object.FindObjectsByType<Turret>(FindObjectsSortMode.None);
        var crawlers = Object.FindObjectsByType<Crawler>(FindObjectsSortMode.None);

        var far = new List<Vector2>();
        var near = new List<Vector2>();

        for (int i = 0; i < samples; i++) {
            Vector2 c = new(Random.Range(area.xMin, area.xMax),
                            Random.Range(area.yMin, area.yMax));
            if (!IsFree(c, p, turrets, crawlers, ignorePlayerDistance: true)) continue;

            if (Vector2.Distance(c, p) >= minDistanceFromPlayer) far.Add(c); else near.Add(c);
        }

        if (far.Count > 0) {
            // Farthest first, then pick inside the leading slice so it still costs travel
            // without always landing on the identical spot.
            far.Sort((a, b) => Vector2.Distance(b, p).CompareTo(Vector2.Distance(a, p)));
            int pool = Mathf.Max(1, Mathf.RoundToInt(far.Count * farthestFraction));
            result = far[Random.Range(0, pool)];
            return true;
        }

        if (near.Count > 0) {
            Vector2 best = near[0];
            foreach (Vector2 c in near)
                if (Vector2.Distance(c, p) > Vector2.Distance(best, p)) best = c;
            result = best;
            return true;
        }

        result = default;
        return false;
    }

    bool IsFree(Vector2 c, Vector2 player, Turret[] turrets, Crawler[] crawlers,
                bool ignorePlayerDistance = false) {
        if (!ignorePlayerDistance && Vector2.Distance(c, player) < minDistanceFromPlayer) return false;

        // One check covers border walls AND pillars - both are on the Wall layer.
        if (Physics2D.OverlapCircle(c, orbRadius, Layers.WallMask) != null) return false;

        foreach (var t in turrets)
            if (Vector2.Distance(c, t.transform.position) < minDistanceFromEnemy) return false;
        foreach (var cr in crawlers)
            if (Vector2.Distance(c, cr.transform.position) < minDistanceFromEnemy) return false;

        foreach (var kv in _live)
            if (kv.Value != null && Vector2.Distance(c, kv.Value.transform.position) < 1.5f)
                return false;

        return true;
    }

}
}
