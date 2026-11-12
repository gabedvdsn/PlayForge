using FarEmerald.PlayForge.Lab;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The AI hero: the Creator Lab's bot (LAB-10), fighting the player's hero with the same ability assets the player
    /// casts. It casts its abilities in the order its hero's Local Data gives, each when the ability's own Local Data says
    /// (in reach, hurt, or low), handing each its target in the activation's data, which the abilities' click targeting
    /// takes with no click. Between casts it attacks, and backs off a moment when it's low. The roster's keys say where
    /// in the Local Data each is.
    /// </summary>
    public class DemoAI : LabBot
    {
        private void Awake() => Foe = unit => DemoGame.Current != null ? DemoGame.Current.FoeOf(unit as DemoUnit) : null;
    }
}
