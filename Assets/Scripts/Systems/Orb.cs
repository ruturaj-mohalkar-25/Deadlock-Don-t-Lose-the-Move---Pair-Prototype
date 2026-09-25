using UnityEngine;

namespace Lockdown {

/// <summary>
/// Plan v2 section 5. Touch it to get the direction back; let it run out and the direction
/// is gone (recoverable only via the mercy rule).
/// The countdown ring is MANDATORY - with no audio it is the only warning before what is
/// the harshest event in the game.
/// </summary>
public class Orb : MonoBehaviour {
    public float lifetime = 15f;
    public Transform countdownRing;
    public SpriteRenderer ringRenderer, iconRenderer;

    public Direction Dir { get; private set; }

    const float PopTime = 0.25f;

    float _dieAt;
    float _poppedAt = -1f;
    Vector3 _baseScale = Vector3.one;

    void Awake() { _baseScale = transform.localScale; }   // prefab is 0.8 units (plan section 5)

    public void Init(Direction d, Color c) {
        Dir = d;
        _dieAt = Time.unscaledTime + lifetime;   // unscaled: should feel like 15 real seconds
        if (ringRenderer != null) ringRenderer.color = c;
        if (iconRenderer != null) {
            iconRenderer.color = c;
            iconRenderer.transform.localRotation = Quaternion.Euler(0, 0, d switch {
                Direction.Up => 0f, Direction.Right => -90f, Direction.Down => 180f, _ => 90f });
        }
    }

    void Update() {
        float left = _dieAt - Time.unscaledTime;
        if (left <= 0f) { Despawn(); return; }

        if (countdownRing != null) {
            float t = left / lifetime;
            countdownRing.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.15f, t);
        }
        // Pulse AROUND the prefab's own scale. Assigning Vector3.one here threw away the
        // 0.8 the prefab was built at, so every orb rendered 25% oversized.
        float pulse = 1f + 0.2f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / 0.8f));
        // Grows back in after a relocation so the jump reads as deliberate, not a glitch.
        float pop = _poppedAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - _poppedAt) / PopTime);
        transform.localScale = _baseScale * pulse * pop;
    }

    /// <summary>Moved by OrbSpawner when a newer loss left this orb unreachable. The timer
    /// keeps running - relocation fixes fairness, it isn't a refund.</summary>
    public void Relocate(Vector2 to) {
        transform.position = to;
        _poppedAt = Time.unscaledTime;
    }

    void Despawn() {
        if (DirectionSystem.I != null) DirectionSystem.I.MarkPermanent(Dir);
        Destroy(gameObject);
    }

    /// <summary>Mercy rule sub-rule 2: retired silently, no permanent-loss penalty.
    /// It has been redeemed.</summary>
    public void RetireSilently() => Destroy(gameObject);

    void OnTriggerEnter2D(Collider2D other) {
        if (other.gameObject.layer != Layers.Player) return;
        if (DirectionSystem.I != null) DirectionSystem.I.Restore(Dir);
        if (CameraShake.I != null) CameraShake.I.Shake(0.08f, 0.1f);
        Destroy(gameObject);
    }
}
}
