using System.Collections.Generic;
using UnityEngine;

namespace TrashTD.UI
{
    /// <summary>
    /// Finds the named elements of a panel that is either built in code or instantiated from a baked prefab (see
    /// UiPrefabBaker). Collects what is missing so the caller can log one clear error and fall back to the code layout.
    /// </summary>
    public sealed class UiPrefabLookup
    {
        private readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
        private readonly List<string> missingNames = new List<string>();

        public UiPrefabLookup(GameObject root)
        {
            Root = root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) byName.TryAdd(child.name, child);
        }

        public GameObject Root { get; }

        /// <summary>The component on the element with this name, or null (and the name is reported as missing).</summary>
        public T Find<T>(string objectName) where T : Component
        {
            if (byName.TryGetValue(objectName, out Transform found))
            {
                T component = found.GetComponent<T>();
                if (component != null) return component;
            }
            missingNames.Add(objectName + " (" + typeof(T).Name + ")");
            return null;
        }

        /// <summary>The component on the root itself (for example its CanvasGroup), or null (reported as missing).</summary>
        public T FindOnRoot<T>() where T : Component
        {
            T component = Root.GetComponent<T>();
            if (component == null) missingNames.Add(typeof(T).Name + " on the root");
            return component;
        }

        /// <summary>True when an element with this name exists (not reported as missing otherwise).</summary>
        public bool Has(string objectName) => byName.ContainsKey(objectName);

        public bool IsComplete => missingNames.Count == 0;

        /// <summary>A comma-separated list of what was not found.</summary>
        public string Missing => string.Join(", ", missingNames);
    }
}
