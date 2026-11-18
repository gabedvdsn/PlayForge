namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Marks PlayForge's managers, ProcessControl and GameRoot, which a Bootstrapper brings up: <c>Bootstrap</c> makes
    /// the one it's called on the one instance, or destroys it where another is already. A manager that is a GAS,
    /// GameRoot, isn't one of the game's entities: area searches pass it by, and the Mono Entities counter leaves it
    /// out.
    /// </summary>
    public interface IManagerial
    {
        public void Bootstrap();
    }
}
