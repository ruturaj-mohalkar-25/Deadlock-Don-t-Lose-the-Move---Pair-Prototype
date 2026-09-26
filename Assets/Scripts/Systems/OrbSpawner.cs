using System.Collections.Generic;
using UnityEngine;

namespace Lockdown {

/// <summary>
/// Plan v2 section 5. One orb per lost direction, max 4.
///
/// Every orb must be reachable with the directions the player has left (OrbPlacement).
/// When a new loss strands an orb that was fine when it spawned, that orb is moved to a
/// reachable spot and keeps its remaining timer.
/// </summary>
public class OrbSpawner : MonoBehaviour {
    public static OrbSpawner I { get; private set; }

    public Orb orbPrefab;
    [Tooltip("Hand-placed spots, tried before the generated grid.")]
    public Transform[] zones = new Transform[8];
    [Tooltip("Orbs keep at least this far from turrets.")]
    public float minDistance = 3f;

    [Header("Placement")]
    [Tooltip("Half-size of the walkable arena interior.")]
    public Vector2 arenaHalfSize = new(10f, 6f);
    [Tooltip("Fraction of arena width/height kept clear along each wall.")]
    [Range(0f, 0.3f)] public float edgeMargin = 0.10f;
    [Tooltip("Preferred distance from the player: 25-40% of arena width.")]
    public float minTravel = 5f, maxTravel = 8f;
    [Tooltip("Orbs keep at least this far from the crawler's patrol line.")]
    public float crawlerPathClearance = 1.5f;
    public float gridStep = 0.5f;

    public static readonly Color[] OrbColors = {
        new(0.20f, 0.53f, 1.00f),   // Up    #3388FF
        new(1.00f, 0.20f, 0.20f),   // Down  #FF3333
        new(1.00f, 0.87f, 0.20f),   // Left  #FFDD33
        new(0.20f, 1.00f, 0.53f),   // Right #33FF88
    };

    // Player collider is radius 0.4; casting a hair thinner keeps a player resting against a
    // pillar from counting as "blocked" before they've moved.
    const float PathRadius  = 0.3f;
    const float SolidRadius = 0.6f;   // orb radius 0.4 + a little air
    const float WallInset   = 0.7f;   // orb centre never closer to a wall than this

    readonly Dictionary<Direction, Orb> _live = new();
    List<Vector2> _grid;

    void Awake() { I = this; }
    void OnDestroy() { if (I == this) I = null; }

    void OnEnable()  { if (DirectionSystem.I != null) DirectionSystem.I.OnMercyAward += Retire; }
    void OnDisable() { if (DirectionSystem.I != null) DirectionSystem.I.OnMercyAward -= Retire; }

    public void Retire(Direction d) {
        if (_live.TryGetValue(d, out var orb) && orb != null) orb.RetireSilently();
        _live.Remove(d);
    }

    /// <summary>The live orb for a direction, or null. Test hook.</summary>
    public Orb LiveOrb(Direction d) => _live.TryGetValue(d, out var o) && o != null ? o : null;

    /// <summary>Call AFTER the direction has been removed from DirectionSystem.</summary>
    public void SpawnFor(Direction d) {
        if (d == Direction.None) return;

        // The loss that got us here may have stranded older orbs. Fix those first so the
        // new orb's spacing check sees where they really are.
        RelocateStranded();

        if (orbPrefab == null) return;
        if (_live.TryGetValue(d, out var existing) && existing != null) return;   // one per direction

        if (!PickSpot(null, out Vector2 at)) at = PlayerPos();   // see PickSpot

        Orb orb = Instantiate(orbPrefab, at, Quaternion.identity);
        orb.Init(d, OrbColors[(int)d]);
        _live[d] = orb;
    }

    /// <summary>Can the player walk to this point with the directions they have left?</summary>
    public bool IsReachable(Vector2 p) {
        var q = BuildQuery(null);
        return OrbPlacement.CanReach(q.player, p, q.canMove, q.pickupReach, q.pathClear);
    }

    void RelocateStranded() {
        foreach (var orb in new List<Orb>(_live.Values)) {
            if (orb == null || IsReachable(orb.transform.position)) continue;
            if (PickSpot(orb, out Vector2 at)) orb.Relocate(at);
        }
    }

    /// <summary>
    /// Tiered search (OrbPlacement.TryPick). Returns false only when no reachable spot exists
    /// anywhere - e.g. pinned in a corner with the one live key pointing into the wall. The
    /// caller then drops the orb on the player, which hands the direction straight back:
    /// the only fair outcome when there is nowhere to send them.
    /// </summary>
    bool PickSpot(Orb ignore, out Vector2 spot) {
        var q = BuildQuery(ignore);
        _grid ??= OrbPlacement.Grid(q.arena, gridStep);

        var preferred = new List<Vector2>();
        foreach (var z in zones) if (z != null) preferred.Add(z.position);

        return OrbPlacement.TryPick(q, preferred, _grid, n => Random.Range(0, n), out spot, out _);
    }

    OrbPlacement.Query BuildQuery(Orb ignore) {
        Vector2 half   = arenaHalfSize;
        Vector2 inner  = half - Vector2.one * WallInset;
        Vector2 pref   = half - half * 2f * edgeMargin;

        var q = new OrbPlacement.Query {
            player    = PlayerPos(),
            canMove   = MovableDirections(),
            arena     = new Rect(-inner, inner * 2f),
            preferred = new Rect(-pref, pref * 2f),
            minTravel = minTravel,
            maxTravel = maxTravel,
            solidAt   = p => Physics2D.OverlapCircle(p, SolidRadius, Layers.WallMask) != null,
            pathClear = PathClear,
        };

        foreach (var t in Object.FindObjectsByType<Turret>(FindObjectsSortMode.None))
            q.hazards.Add(new OrbPlacement.Hazard(t.transform.position, t.transform.position, minDistance));
        foreach (var c in Object.FindObjectsByType<Crawler>(FindObjectsSortMode.None)) {
            q.hazards.Add(new OrbPlacement.Hazard(c.PathStart(), c.PathEnd(), crawlerPathClearance));
        }

        foreach (var orb in _live.Values)
            if (orb != null && orb != ignore) q.taken.Add(orb.transform.position);

        return q;
    }

    static bool PathClear(Vector2 a, Vector2 b) {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 1e-4f) return true;
        return !Physics2D.CircleCast(a, PathRadius, d / len, len, Layers.WallMask);
    }

    static Vector2 PlayerPos() {
        var player = Object.FindFirstObjectByType<PlayerController>();
        return player != null ? (Vector2)player.transform.position : Vector2.zero;
    }

    /// <summary>
    /// The directions to plan around. With all four gone the mercy rule hands one back within
    /// two seconds, so the orb is placed for THAT direction rather than for nothing at all.
    /// </summary>
    static bool[] MovableDirections() {
        var can = new bool[Dir.Count];
        var ds = DirectionSystem.I;
        if (ds == null) { for (int i = 0; i < Dir.Count; i++) can[i] = true; return can; }

        bool any = false;
        for (int i = 0; i < Dir.Count; i++) { can[i] = ds.IsActive((Direction)i); any |= can[i]; }
        if (!any && ds.NextMercyAward != Direction.None) can[(int)ds.NextMercyAward] = true;
        return can;
    }
}
}
