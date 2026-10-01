using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Scene-scoped singleton. Not persisted across scene loads, so a run restart
    /// simply reloads the Gameplay scene and every system rebuilds itself.
    /// </summary>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate {typeof(T).Name} on '{name}', destroying it.", this);
                Destroy(this);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
