using System.Collections.Generic;
using UnityEngine;

namespace Lockdown {

/// <summary>Plan v2 section 5. One orb per lost direction, max 4.</summary>
public class OrbSpawner : MonoBehaviour {
    public static OrbSpawner I { get; private set; }

    public Orb orbPrefab;
    public Transform[] zones = new Transform[8];
    public float minDistance = 3f;

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

        Transform zone = PickZone();
        if (zone == null) return;

        Orb orb = Instantiate(orbPrefab, zone.position, Quaternion.identity);
        orb.Init(d, OrbColors[(int)d]);
        _live[d] = orb;
    }

    /// <summary>
    /// Valid = not within 3u of the player, not within 3u of an enemy, not already occupied.
    /// FALLBACK (decision B8): if nothing is valid, take the zone farthest from the player and
    /// ignore the distance rule. Never returns null on a populated zone list - without this the
    /// hit that should have spawned an orb silently spawns nothing.
    /// </summary>
    Transform PickZone() {
        var player = Object.FindFirstObjectByType<PlayerController>();
        Vector2 p = player != null ? (Vector2)player.transform.position : Vector2.zero;
        var enemies = Object.FindObjectsByType<Crawler>(FindObjectsSortMode.None);
        var turrets = Object.FindObjectsByType<Turret>(FindObjectsSortMode.None);

        var valid = new List<Transform>();
        Transform farthest = null; float farthestD = -1f;

        foreach (var z in zones) {
            if (z == null) continue;
            Vector2 zp = z.position;

            float dp = Vector2.Distance(zp, p);
            if (dp > farthestD) { farthestD = dp; farthest = z; }

            if (IsOccupied(z)) continue;
            if (dp < minDistance) continue;
            bool near = false;
            foreach (var c in enemies) if (Vector2.Distance(zp, c.transform.position) < minDistance) { near = true; break; }
            if (!near) foreach (var t in turrets) if (Vector2.Distance(zp, t.transform.position) < minDistance) { near = true; break; }
            if (near) continue;

            valid.Add(z);
        }

        if (valid.Count > 0) return valid[Random.Range(0, valid.Count)];
        return farthest;
    }

    bool IsOccupied(Transform zone) {
        foreach (var kv in _live)
            if (kv.Value != null && Vector2.Distance(kv.Value.transform.position, zone.position) < 0.1f)
                return true;
        return false;
    }
}
}
