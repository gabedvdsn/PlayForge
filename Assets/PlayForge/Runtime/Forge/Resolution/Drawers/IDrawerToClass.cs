namespace FarEmerald.PlayForge.Extended.Editor
{
    public interface IDrawerToClass<out T> where T : class
    {
        public T Generate();
    }
}
