using UnityEngine;

namespace Lockdown {

/// <summary>
/// Plan v2 section 6. Aim LOCKS at telegraph start, and the turret always fires - no LOS
/// re-check at the fire moment.
///
/// Why: it makes the 0.5s glow a real dodge window. If shots are dodged by MOVING, then
/// losing a direction directly reduces your ability to dodge, which costs more directions.
/// The death spiral is the core loop and it only exists if movement is the counterplay.
/// A player who ducks behind a pillar gets a bullet fired at their old position, which hits
/// the pillar and dies - cover works through plain physics, with no abort state to write.
/// </summary>
public class Turret : MonoBehaviour {
    public Bullet bulletPrefab;
    public Transform barrel, barrelTip;
    public SpriteRenderer core, body;

    public float range = 15f;
    public float telegraph = 0.5f;          // CONSTANT - never ramps, it is the dodge window
    public float spawnProtection = 1.0f;

    float _cycleEnd, _fireAt = -1f, _respawnAt = -1f, _protectedUntil = -1f;
    Vector2 _lockedAim;
    bool _alive = true;
    Collider2D _col;

    void Awake() { _col = GetComponent<Collider2D>(); }

    void Update() {
        if (LevelManager.I == null || LevelManager.I.Frozen) return;

        if (!_alive) {
            if (Time.time >= _respawnAt) Respawn();
            return;
        }

        if (_fireAt > 0f) {                                  // telegraphing
            if (core != null) core.color = Color.Lerp(new Color(1f,0.53f,0.33f), Color.white,
                                            1f - Mathf.Max(0f, (_fireAt - Time.time) / telegraph));
            if (Time.time >= _fireAt) Fire();
            return;
        }

        if (Time.time >= _cycleEnd) TryAcquire();
    }

    void TryAcquire() {
        var player = FindPlayer();
        if (player == null) return;

        Vector2 to = (Vector2)player.position - (Vector2)transform.position;
        if (to.magnitude > range) return;
        if (Physics2D.Raycast(transform.position, to.normalized, to.magnitude, Layers.WallMask)) return;

        _lockedAim = to.normalized;                          // <<< AIM LOCKS HERE
        _fireAt = Time.time + telegraph;
        if (barrel != null) barrel.right = _lockedAim;
    }

    void Fire() {
        _fireAt = -1f;
        _cycleEnd = Time.time + LevelManager.I.TurretFireGap;   // captured per cycle, not read live
        if (core != null) core.color = new Color(1f, 0.53f, 0.33f);
        if (bulletPrefab == null) return;

        Vector3 origin = barrelTip != null ? barrelTip.position
                       : transform.position + (Vector3)(_lockedAim * 0.6f);
        Instantiate(bulletPrefab, origin, Quaternion.identity).Fire(_lockedAim, enemy: true);
    }

    /// <summary>A spawn-protected turret survives and flashes; the bullet dies either way
    /// (decision B7 - passing through reads as a hit-detection bug).</summary>
    public void TakeHit() {
        if (!_alive || Time.time < _protectedUntil) { Flash(); return; }
        Die();
    }

    void Flash() { if (body != null) body.color = Color.white; Invoke(nameof(ResetTint), 0.06f); }
    void ResetTint() { if (body != null) body.color = new Color(1f, 0.33f, 0.2f); }

    void Die() {
        _alive = false;
        _fireAt = -1f;
        _respawnAt = Time.time + LevelManager.I.TurretRespawn;   // captured at death
        SetVisible(false);
        Hitstop.Freeze(0.05f);
    }

    void Respawn() {
        _alive = true;
        _protectedUntil = Time.time + spawnProtection;
        _cycleEnd = Time.time;
        SetVisible(true);
    }

    void SetVisible(bool on) {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = on;
        if (_col != null) _col.enabled = on;
    }

    static Transform FindPlayer() {
        var p = Object.FindFirstObjectByType<PlayerController>();
        return p != null ? p.transform : null;
    }
}
}
