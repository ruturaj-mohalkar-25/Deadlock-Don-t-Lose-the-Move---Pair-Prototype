using UnityEngine;

namespace Lockdown {

/// <summary>Plan v2 section 8/9. Mouse aim, independent of movement - you can always shoot
/// in a direction you can no longer move in.</summary>
public class PlayerShooting : MonoBehaviour {
    public Bullet bulletPrefab;
    public Transform firePoint;
    public float fireRate = 0.25f;

    float _next;

    void Update() {
        if (LevelManager.I != null && LevelManager.I.Frozen) return;
        if (!Input.GetMouseButton(0) || Time.time < _next) return;
        if (bulletPrefab == null) return;

        _next = Time.time + fireRate;

        Vector3 m = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = ((Vector2)(m - transform.position)).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        Vector3 spawn = firePoint != null ? firePoint.position : transform.position;
        Bullet b = Instantiate(bulletPrefab, spawn + (Vector3)(dir * 0.45f), Quaternion.identity);
        b.Fire(dir, enemy: false);

        if (CameraShake.I != null) CameraShake.I.Shake(0.06f, 0.05f);
    }
}
}
