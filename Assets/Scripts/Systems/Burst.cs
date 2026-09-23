using UnityEngine;

namespace Lockdown {

/// <summary>
/// One-shot particle burst, built entirely in code so it needs no prefab and no external art.
///
/// The system destroys itself when it finishes (stopAction = Destroy), so callers fire and
/// forget. First particle effect in the project - fin shatter, orb pickup and the death
/// explosion can all reuse this.
/// </summary>
public static class Burst {

    public static void Play(Vector2 at, Color color, int count = 30,
                            float speed = 6f, float size = 0.22f, float life = 1.1f) {
        var go = new GameObject("Burst") { transform = { position = at } };

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();                                   // AddComponent auto-plays; configure first

        var main = ps.main;
        main.duration        = 0.2f;
        main.loop            = false;
        main.playOnAwake     = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize       = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor      = color;
        main.gravityModifier = 0.5f;                 // let them arc and fall - reads as celebration
        main.useUnscaledTime = true;                 // must survive hitstop and the win freeze
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.15f;

        // Fade out rather than vanishing mid-air.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material     = new Material(Shader.Find("Sprites/Default"));
        r.sortingOrder = 50;                         // above everything in the arena

        ps.Play();
    }
}
}
