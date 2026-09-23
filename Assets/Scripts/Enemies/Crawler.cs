using UnityEngine;

namespace Lockdown {

/// <summary>
/// Plan v2 section 7. Hazard, not an enemy - unkillable, instant death on contact.
/// Speed ramps 2.0 -> 4.5 u/s; the cap stays under the player's 5.0 so it is always
/// outrunnable, which matters because contact bypasses hearts entirely.
/// </summary>
public class Crawler : MonoBehaviour {
    public Transform pointA, pointB;
    public SpriteRenderer body;

    Vector2 _a, _b, _target;
    float _pauseUntil;

    void Start() {
        _a = pointA != null ? (Vector2)pointA.position : (Vector2)transform.position + Vector2.left * 3f;
        _b = pointB != null ? (Vector2)pointB.position : (Vector2)transform.position + Vector2.right * 3f;
        transform.position = _a;
        _target = _b;
    }

    void Update() {
        if (LevelManager.I == null || LevelManager.I.Frozen) return;
        if (Time.time < _pauseUntil) return;

        float speed = LevelManager.I.CrawlerSpeed;
        transform.position = Vector2.MoveTowards(transform.position, _target, speed * Time.deltaTime);

        // Tint brightens per tier - with no audio this is the only cue that it sped up.
        if (body != null)
            body.color = Color.Lerp(new Color(1f, 0.2f, 0.67f), Color.white, LevelManager.I.Tier / 10f);

        if (((Vector2)transform.position - _target).sqrMagnitude < 0.0001f) {
            // Pause scales with speed: a fixed 0.5s means a 4.5 u/s crawler stands still for
            // over a quarter of its cycle, which looks broken and blunts the escalation.
            _pauseUntil = Time.time + 0.5f * (2f / speed);
            _target = _target == _a ? _b : _a;
        }
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (other.gameObject.layer != Layers.Player) return;
        var hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null) hp.KillByCrawler();
    }
}
}
