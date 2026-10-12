namespace KingsRescue.Core
{
    /// <summary>
    /// Implemented by components on pooled prefabs that need to reset state
    /// between reuses. Called by <see cref="ObjectPoolManager"/>.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();

        void OnDespawned();
    }
}
