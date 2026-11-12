using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The player's controls, as a MOBA's: right-click the ground to move, right-click an enemy to attack it, Q, W and E for
    /// the hero's abilities, 1 to 6 for its items' abilities, S to stop, Escape to cancel a cast. An ability that needs a
    /// target waits for a left-click, as its own click targeting reads it. It reads PlayForge's input, ForgeInput, as the
    /// Creator Lab does, so a test or a game's own input stands in.
    /// </summary>
    public class DemoPlayer : MonoBehaviour
    {
        [Tooltip("The camera the pointer aims through; with none, the main camera")]
        public Camera Camera;

        [Tooltip("The layers a right-click finds the ground or a unit on")]
        public LayerMask Layers = ~0;

        private void Update()
        {
            var game = DemoGame.Current;
            var me = game != null ? game.Player : null;
            if (me == null || !me.Alive) return;

            var input = ForgeInput.Current;
            if (input.PointerPressed(1) && input.TryGetPointerPosition(out var pointer) && !DemoHud.PointerOverPanel(pointer)) Order(me, pointer);
            if (input.KeyPressed(KeyCode.Q)) me.Cast(me.AbilityAt(1));
            if (input.KeyPressed(KeyCode.W)) me.Cast(me.AbilityAt(2));
            if (input.KeyPressed(KeyCode.E)) me.Cast(me.AbilityAt(3));
            for (int i = 0; i < 6; i++)
                if (input.KeyPressed(KeyCode.Alpha1 + i)) me.Cast(me.ItemAbilityAt(i));
            if (input.KeyPressed(KeyCode.S)) me.Stop();
            if (input.KeyPressed(KeyCode.Escape)) me.CancelCasts();
        }

        // Right-click: an enemy under the pointer to attack, or the ground to walk to
        private void Order(DemoUnit me, Vector2 screen)
        {
            var aim = Camera != null ? Camera : Camera.main;
            if (aim == null) return;
            if (!Physics.Raycast(aim.ScreenPointToRay(screen), out var hit, 500f, Layers, QueryTriggerInteraction.Ignore)) return;
            var unit = hit.collider.GetComponentInParent<DemoUnit>();
            if (unit != null && unit != me && !DemoTeams.AreAllies(me.Gas, unit.Gas) && me.CanTarget(unit)) me.Attack(unit);
            else me.MoveTo(hit.point);
        }
    }
}
