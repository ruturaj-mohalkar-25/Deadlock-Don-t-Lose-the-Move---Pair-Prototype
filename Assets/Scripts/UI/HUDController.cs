using UnityEngine;
using UnityEngine.UI;

namespace Lockdown {

/// <summary>Plan v2 section 11. Pure iconography, no labels. Hearts and arrows are
/// deliberately different resources and will visibly desync - that is intended.</summary>
public class HUDController : MonoBehaviour {
    public Text[] arrows = new Text[4];   // Up, Down, Left, Right
    public Text hearts, timer, banner;

    static readonly Color Lost      = new(0.5f, 0.5f, 0.5f, 0.6f);
    static readonly Color Permanent = new(0.28f, 0.28f, 0.28f, 0.9f);

    void OnEnable() {
        if (DirectionSystem.I != null) {
            DirectionSystem.I.OnLost      += Refresh;
            DirectionSystem.I.OnRestored  += Refresh;
            DirectionSystem.I.OnPermanent += Refresh;
        }
        if (LevelManager.I != null) LevelManager.I.OnStateChanged += ShowBanner;
        var hp = Object.FindFirstObjectByType<PlayerHealth>();
        if (hp != null) hp.OnHeartsChanged += SetHearts;
    }

    void Start() { for (int i = 0; i < 4; i++) Refresh((Direction)i); if (banner != null) banner.text = ""; }

    void Update() {
        if (timer == null || LevelManager.I == null) return;
        float r = LevelManager.I.Remaining;
        timer.text = $"{Mathf.FloorToInt(r / 60f)}:{Mathf.FloorToInt(r % 60f):00}";
        timer.color = r < 10f ? Color.red : Color.white;
    }

    void Refresh(Direction d) {
        int i = (int)d;
        if (arrows == null || i < 0 || i >= arrows.Length || arrows[i] == null) return;
        var ds = DirectionSystem.I;
        arrows[i].color = ds.IsActive(d)    ? OrbSpawner.OrbColors[i]
                        : ds.IsPermanent(d) ? Permanent
                                            : Lost;
    }

    void SetHearts(int n) {
        if (hearts != null) hearts.text = new string('♥', Mathf.Max(0, n));
    }

    void ShowBanner(GameState s) {
        if (banner == null) return;
        banner.text = s == GameState.Won ? "YOU WIN" : s == GameState.Dead ? "YOU DIED" : "";
        banner.color = s == GameState.Won ? new Color(1f, 0.85f, 0.3f) : Color.red;
    }
}
}
