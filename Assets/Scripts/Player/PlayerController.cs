using UnityEngine;

namespace Lockdown {

/// <summary>
/// Plan v2 section 3. Movement with the direction lock, plus the fin visuals.
///
/// Rigidbody2D MUST be Dynamic with Gravity Scale 0 (plan v2 section 13). A Kinematic
/// body does not resolve collisions - you walk straight through pillars - and Unity's
/// Dynamic default is Gravity Scale 1, which slowly drags you to the bottom of the arena.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour {
    public float speed = 5f;

    [Header("Fins (index order: Up, Down, Left, Right)")]
    public SpriteRenderer[] fins = new SpriteRenderer[4];
    public Transform aimDot;
    public float aimDotRadius = 0.22f;

    Rigidbody2D _rb;

    void Awake() {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType     = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    /// <summary>
    /// Subscribes in Start, NOT OnEnable.
    ///
    /// Awake/OnEnable ordering between separate GameObjects is undefined, and the player and
    /// the Systems object are separate. If the player initialised first, DirectionSystem.I was
    /// still null, the old OnEnable bailed out silently, and the fins never hid for the entire
    /// run. Start is guaranteed to run after every Awake, so the singleton always exists.
    /// </summary>
    void Start() {
        if (DirectionSystem.I == null) return;
        DirectionSystem.I.OnLost      += RefreshFin;
        DirectionSystem.I.OnRestored  += RefreshFin;
        DirectionSystem.I.OnPermanent += RefreshFin;
        for (int i = 0; i < Dir.Count; i++) RefreshFin((Direction)i);   // seed initial state
    }

    void OnDisable() {
        if (DirectionSystem.I == null) return;
        DirectionSystem.I.OnLost      -= RefreshFin;
        DirectionSystem.I.OnRestored  -= RefreshFin;
        DirectionSystem.I.OnPermanent -= RefreshFin;
    }

    void Update() {
        if (aimDot == null) return;

        // The aim dot doubles as the ARMED light. Firing is locked until a direction is
        // lost, and a click that silently does nothing reads as broken input, so the dot
        // only appears once the gun is live.
        var shooting = GetComponent<PlayerShooting>();
        bool armed = shooting == null || shooting.Armed;
        if (_aimDotSr == null) _aimDotSr = aimDot.GetComponent<SpriteRenderer>();
        if (_aimDotSr != null) _aimDotSr.enabled = armed;
        if (!armed) return;

        // Offsets toward the mouse - a centred dot cannot show a direction.
        Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = ((Vector2)(m - transform.position)).normalized;
        aimDot.localPosition = dir * aimDotRadius;
    }

    SpriteRenderer _aimDotSr;

    void FixedUpdate() {
        if (LevelManager.I != null && LevelManager.I.Frozen) { _rb.linearVelocity = Vector2.zero; return; }

        Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        var ds = DirectionSystem.I;
        if (ds != null) {
            if (!ds.IsActive(Direction.Left)  && input.x < 0) input.x = 0;
            if (!ds.IsActive(Direction.Right) && input.x > 0) input.x = 0;
            if (!ds.IsActive(Direction.Down)  && input.y < 0) input.y = 0;
            if (!ds.IsActive(Direction.Up)    && input.y > 0) input.y = 0;
        }

        // CRITICAL (plan v2 section 3): without this, diagonals run at 7.07 u/s and
        // losing a direction makes you FASTER, inverting the entire mechanic.
        input = Vector2.ClampMagnitude(input, 1f);

        _rb.linearVelocity = input * speed;
    }

    void RefreshFin(Direction d) {
        if (fins == null || (int)d < 0 || (int)d >= fins.Length) return;
        var sr = fins[(int)d];
        if (sr != null) sr.enabled = DirectionSystem.I.IsActive(d);
    }
}
}
