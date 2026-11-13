using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The player's controls, as a MOBA's: right-click the ground to move, right-click an enemy to attack it, Q, W and E for
    /// the hero's abilities, 1 to 6 for its items' abilities, S to stop, Escape to cancel a cast. An ability that needs a
    /// target waits for a left-click, as its own click targeting reads it. They're the Creator Lab's controls (LAB-11),
    /// set on the Demo's hero, its items by its seat, and its HUD's panels keeping their clicks; they read PlayForge's
    /// input, ForgeInput, so a test or a game's own input stands in.
    /// </summary>
    public class DemoPlayer : LabControls
    {
        protected override LabUnit ControlledUnit()
        {
            var game = DemoGame.Current;
            return game != null ? game.Player : null;
        }

        protected override Ability ItemAbility(LabUnit unit, int index) =>
            unit is DemoUnit hero ? hero.ItemAbilityAt(index) : base.ItemAbility(unit, index);

        protected override bool PointerOverPanel(Vector2 screen) => DemoHud.PointerOverPanel(screen);
    }
}
