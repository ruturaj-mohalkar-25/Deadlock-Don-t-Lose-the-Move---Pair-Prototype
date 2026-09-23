using System;
using NUnit.Framework;
using UnityEngine;
using Lockdown;

namespace Lockdown.Tests {

/// <summary>
/// An orb the player cannot reach is worse than no orb: it is a guaranteed permanent direction
/// loss dressed up as a lifeline.
///
/// Movement is axis-locked with no screen wrap, so a lost direction permanently bounds where
/// the player can ever go again - lose Left and x can never decrease. These tests pin down
/// that the spawn rectangle respects those bounds.
/// </summary>
public class OrbSpawnTests {

    static readonly Vector2 Half = new(9.4f, 5.4f);   // OrbSpawner.arenaHalfExtents default
    const float Tol = 0.6f;                            // pickupTolerance default

    static Func<Direction, bool> Active(params Direction[] lost) =>
        d => Array.IndexOf(lost, d) < 0;

    static Rect Area(Vector2 player, params Direction[] lost) =>
        OrbSpawner.ReachableArea(player, Half, Active(lost), Tol);

    // ---------------------------------------------------------------- the reported bug

    [Test]
    public void LosingLeftNeverPlacesTheOrbToTheLeft() {
        // The exact failure that was reported: only Up and Down remained, and the orb landed
        // to the left where the player could never travel.
        Vector2 p = new(0f, 0f);
        Rect r = Area(p, Direction.Left);
        Assert.GreaterOrEqual(r.xMin, p.x - Tol, "spawn area must not extend left of the player");
        Assert.AreEqual(Half.x, r.xMax, 0.0001f, "rightward space must stay fully available");
    }

    [Test]
    public void LosingLeftAndRightLeavesOnlyAVerticalCorridor() {
        Vector2 p = new(2f, -1f);
        Rect r = Area(p, Direction.Left, Direction.Right);
        Assert.LessOrEqual(r.width, Tol * 2f + 0.0001f, "horizontal movement is gone");
        Assert.AreEqual(-Half.y, r.yMin, 0.0001f);
        Assert.AreEqual( Half.y, r.yMax, 0.0001f);
    }

    [TestCase(Direction.Left)]
    [TestCase(Direction.Right)]
    [TestCase(Direction.Up)]
    [TestCase(Direction.Down)]
    public void ASingleLossOnlyClampsItsOwnAxis(Direction lost) {
        Vector2 p = new(1f, 2f);
        Rect r = Area(p, lost);
        bool horizontal = lost == Direction.Left || lost == Direction.Right;
        if (horizontal) {
            Assert.AreEqual(-Half.y, r.yMin, 0.0001f, "vertical range must be untouched");
            Assert.AreEqual( Half.y, r.yMax, 0.0001f);
        } else {
            Assert.AreEqual(-Half.x, r.xMin, 0.0001f, "horizontal range must be untouched");
            Assert.AreEqual( Half.x, r.xMax, 0.0001f);
        }
    }

    // ---------------------------------------------------------------- general shape

    [Test]
    public void FullMobilityCanUseTheWholeArena() {
        Rect r = Area(new Vector2(3f, 1f));
        Assert.AreEqual(-Half.x, r.xMin, 0.0001f);
        Assert.AreEqual( Half.x, r.xMax, 0.0001f);
        Assert.AreEqual(-Half.y, r.yMin, 0.0001f);
        Assert.AreEqual( Half.y, r.yMax, 0.0001f);
    }

    [Test]
    public void EveryPointInTheAreaIsActuallyReachable() {
        // Sweep the rect and confirm no sample requires a movement the player no longer has.
        Vector2 p = new(-2f, 3f);
        Rect r = Area(p, Direction.Up, Direction.Right);
        for (int i = 0; i <= 20; i++)
        for (int j = 0; j <= 20; j++) {
            Vector2 c = new(Mathf.Lerp(r.xMin, r.xMax, i / 20f),
                            Mathf.Lerp(r.yMin, r.yMax, j / 20f));
            Assert.LessOrEqual(c.x, p.x + Tol + 0.0001f, $"{c} needs Right, which is lost");
            Assert.LessOrEqual(c.y, p.y + Tol + 0.0001f, $"{c} needs Up, which is lost");
        }
    }

    [Test]
    public void AreaNeverInvertsWhenThePlayerIsOutsideNominalBounds() {
        // Player pushed slightly past the sampling bound: width/height must clamp to zero,
        // never go negative, or Random.Range would produce points outside the arena.
        Rect r = Area(new Vector2(20f, 20f), Direction.Left, Direction.Down);
        Assert.GreaterOrEqual(r.width, 0f);
        Assert.GreaterOrEqual(r.height, 0f);
    }

    [Test]
    public void LosingAllFourCollapsesToThePlayer() {
        Vector2 p = new(1f, 1f);
        Rect r = Area(p, Direction.Up, Direction.Down, Direction.Left, Direction.Right);
        Assert.LessOrEqual(r.width,  Tol * 2f + 0.0001f);
        Assert.LessOrEqual(r.height, Tol * 2f + 0.0001f);
        // The mercy rule is what actually rescues this state; the spawner just must not crash.
    }
}
}
