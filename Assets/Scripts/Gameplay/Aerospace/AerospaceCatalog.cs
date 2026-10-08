using System;
using UnityEngine;

namespace ExtractionLike.Aerospace
{
    [Serializable] public sealed class AerospaceCatalog
    {
        public AerospacePart[] models;
        private static AerospaceCatalog cached;
        public static AerospaceCatalog Load()
        {
            if (cached != null) return cached;
            var json = Resources.Load<TextAsset>("Aerospace/PartsCatalog");
            if (json == null) throw new InvalidOperationException("Aerospace/PartsCatalog is missing. Run AerospaceCollectiblesBuilder.Build.");
            cached = JsonUtility.FromJson<AerospaceCatalog>(json.text);
            return cached;
        }
        public AerospacePart Find(string code) => Array.Find(models, p => p.code == code);
        public static string CodeFor(InventoryItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemID) || !item.ItemID.StartsWith("ASTRA_R0", StringComparison.Ordinal)) return null;
            string code = item.ItemID.Substring(6);
            return Load().Find(code) != null ? code : null;
        }
        public static Vector3 Convert(float[] v) => v != null && v.Length == 3 ? new Vector3(v[0], v[2], v[1]) : Vector3.zero;
    }
    [Serializable] public sealed class AerospacePart
    {
        public string code, title, english, intro, locationTitle, locationText;
        public float[] camera, size;
        public AerospaceMesh[] parts;
        public AerospaceHotspot[] hotspots;
        public AerospaceSource[] sources;
    }
    [Serializable] public sealed class AerospaceMesh { public string name, family; public bool cut, fold; public float[] explode; }
    [Serializable] public sealed class AerospaceHotspot { public string title, text, view, anchor; public string[] groups; }
    [Serializable] public sealed class AerospaceSource { public string label, url; }

    public static class AerospaceUiInputGate
    {
        private static bool modal;
        private static int releasedFrame = -10;
        public static bool BlocksGameplayInput => modal || Time.frameCount <= releasedFrame + 1;
        public static void Set(bool value) { modal = value; if (!value) releasedFrame = Time.frameCount; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { modal = false; releasedFrame = -10; }
    }
}
