using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shiori
{
    public enum UnityObjectChangeKind
    {
        Added,
        Removed,
        Changed,
    }

    /// <summary>One property that differs, as a dotted path into the object (for example m_LocalPosition.x).</summary>
    public sealed class UnityPropertyChange
    {
        public string Path { get; }

        /// <summary>The old value as text, or null when the property is new.</summary>
        public string OldValue { get; }

        /// <summary>The new value as text, or null when the property was removed.</summary>
        public string NewValue { get; }

        public UnityPropertyChange(string path, string oldValue, string newValue)
        {
            Path = path;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>One object (GameObject, component, material, …) that was added, removed or changed.</summary>
    public sealed class UnityObjectChange
    {
        public UnityObjectChangeKind Kind { get; }
        public long FileId { get; }
        public string TypeName { get; }

        /// <summary>
        /// Where the object sits, for people: the GameObject's hierarchy path ("Root/Body/Arm") for
        /// GameObjects and their components, or the object's own m_Name (materials and other assets).
        /// </summary>
        public string Location { get; }

        /// <summary>Changed properties; for added and removed objects, every property with its value.</summary>
        public IReadOnlyList<UnityPropertyChange> Properties { get; }

        public UnityObjectChange(UnityObjectChangeKind kind, long fileId, string typeName, string location, IReadOnlyList<UnityPropertyChange> properties)
        {
            Kind = kind;
            FileId = fileId;
            TypeName = typeName ?? string.Empty;
            Location = location ?? string.Empty;
            Properties = properties ?? Array.Empty<UnityPropertyChange>();
        }
    }

    /// <summary>Turns a referenced asset GUID into something readable (an asset path), or null when unknown.</summary>
    public interface IGuidResolver
    {
        string Resolve(string guid);
    }

    /// <summary>
    /// Compares two versions of a Unity YAML file object by object (matched by fileID, which Unity keeps
    /// stable) and property by property. Values are shown as text; object references are shown as asset
    /// names through an <see cref="IGuidResolver"/>, or as the referenced object's location in the file.
    /// </summary>
    public static class UnityYamlDiff
    {
        /// <summary>Unity's built-in resources share these GUIDs.</summary>
        public const string BuiltinGuid = "0000000000000000e000000000000000";
        public const string BuiltinExtraGuid = "0000000000000000f000000000000000";

        /// <summary>Fields Unity rewrites without a meaningful change; they would only add noise.</summary>
        private static readonly HashSet<string> IgnoredFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "serializedVersion",
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
        };

        /// <summary>
        /// Differences between <paramref name="oldText"/> and <paramref name="newText"/>. Either may be null
        /// (file added or removed). Objects come in the new file's order, removed ones at the end.
        /// </summary>
        public static IReadOnlyList<UnityObjectChange> Compare(string oldText, string newText, IGuidResolver resolver = null)
        {
            var oldDocs = UnityYamlParser.Parse(oldText);
            var newDocs = UnityYamlParser.Parse(newText);
            var oldById = Index(oldDocs);
            var newById = Index(newDocs);
            var oldNames = new Locator(oldDocs);
            var newNames = new Locator(newDocs);

            var result = new List<UnityObjectChange>();
            foreach (var doc in newDocs)
            {
                if (!oldById.TryGetValue(doc.FileId, out var before))
                {
                    result.Add(new UnityObjectChange(UnityObjectChangeKind.Added, doc.FileId, doc.TypeName, newNames.Locate(doc),
                        AllProperties(doc.Body, newNames, resolver, added: true)));
                    continue;
                }
                var properties = new List<UnityPropertyChange>();
                CompareNode(string.Empty, before.Body, doc.Body, oldNames, newNames, resolver, properties);
                if (properties.Count > 0)
                {
                    result.Add(new UnityObjectChange(UnityObjectChangeKind.Changed, doc.FileId, doc.TypeName, newNames.Locate(doc), properties));
                }
            }
            foreach (var doc in oldDocs)
            {
                if (newById.ContainsKey(doc.FileId)) continue;
                result.Add(new UnityObjectChange(UnityObjectChangeKind.Removed, doc.FileId, doc.TypeName, oldNames.Locate(doc),
                    AllProperties(doc.Body, oldNames, resolver, added: false)));
            }
            return result;
        }

        private static Dictionary<long, UnityYamlDocument> Index(IReadOnlyList<UnityYamlDocument> docs)
        {
            var index = new Dictionary<long, UnityYamlDocument>();
            foreach (var doc in docs) index[doc.FileId] = doc;
            return index;
        }

        private static IReadOnlyList<UnityPropertyChange> AllProperties(YamlNode body, Locator names, IGuidResolver resolver, bool added)
        {
            var list = new List<UnityPropertyChange>();
            Flatten(string.Empty, body, names, resolver, (path, value) => list.Add(added ? new UnityPropertyChange(path, null, value) : new UnityPropertyChange(path, value, null)));
            return list;
        }

        private static void CompareNode(string path, YamlNode before, YamlNode after, Locator oldNames, Locator newNames, IGuidResolver resolver, List<UnityPropertyChange> changes)
        {
            // References compare by what they point at, so a moved fileID in the same object is not reported twice.
            var oldRef = before.AsReference();
            var newRef = after.AsReference();
            if (oldRef != null || newRef != null)
            {
                var o = oldRef != null ? Describe(oldRef, oldNames, resolver) : before.ToString();
                var n = newRef != null ? Describe(newRef, newNames, resolver) : after.ToString();
                if (!SameReference(oldRef, newRef) && o != n) changes.Add(new UnityPropertyChange(path, o, n));
                return;
            }

            if (before.Kind == YamlNodeKind.Map && after.Kind == YamlNodeKind.Map)
            {
                foreach (var entry in after.Entries)
                {
                    if (IgnoredFields.Contains(entry.Key)) continue;
                    var child = Join(path, entry.Key);
                    var previous = before[entry.Key];
                    if (previous == null) Flatten(child, entry.Value, newNames, resolver, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
                    else CompareNode(child, previous, entry.Value, oldNames, newNames, resolver, changes);
                }
                foreach (var entry in before.Entries)
                {
                    if (IgnoredFields.Contains(entry.Key) || after[entry.Key] != null) continue;
                    Flatten(Join(path, entry.Key), entry.Value, oldNames, resolver, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
                }
                return;
            }

            if (before.Kind == YamlNodeKind.Sequence && after.Kind == YamlNodeKind.Sequence)
            {
                CompareSequence(path, before.Items, after.Items, oldNames, newNames, resolver, changes);
                return;
            }

            var oldText = before.Kind == YamlNodeKind.Scalar ? before.Value : before.ToString();
            var newText = after.Kind == YamlNodeKind.Scalar ? after.Value : after.ToString();
            if (!string.Equals(oldText, newText, StringComparison.Ordinal)) changes.Add(new UnityPropertyChange(path, oldText, newText));
        }

        /// <summary>
        /// Material property lists ("- _Color: {…}") are lists of one-key maps; matching those by key
        /// reads as "_Color.r" instead of "[3].r" and survives reordering. Other lists compare by index.
        /// </summary>
        private static void CompareSequence(string path, IReadOnlyList<YamlNode> before, IReadOnlyList<YamlNode> after, Locator oldNames, Locator newNames, IGuidResolver resolver, List<UnityPropertyChange> changes)
        {
            if (IsKeyedList(before) && IsKeyedList(after))
            {
                var oldByKey = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
                foreach (var item in before) oldByKey[item.Entries[0].Key] = item.Entries[0].Value;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in after)
                {
                    var key = item.Entries[0].Key;
                    seen.Add(key);
                    var child = Join(path, key);
                    if (oldByKey.TryGetValue(key, out var previous)) CompareNode(child, previous, item.Entries[0].Value, oldNames, newNames, resolver, changes);
                    else Flatten(child, item.Entries[0].Value, newNames, resolver, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
                }
                foreach (var pair in oldByKey)
                {
                    if (!seen.Contains(pair.Key)) Flatten(Join(path, pair.Key), pair.Value, oldNames, resolver, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
                }
                return;
            }

            var common = Math.Min(before.Count, after.Count);
            for (var i = 0; i < common; i++) CompareNode(Index(path, i), before[i], after[i], oldNames, newNames, resolver, changes);
            for (var i = common; i < after.Count; i++) Flatten(Index(path, i), after[i], newNames, resolver, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
            for (var i = common; i < before.Count; i++) Flatten(Index(path, i), before[i], oldNames, resolver, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
        }

        private static bool IsKeyedList(IReadOnlyList<YamlNode> items)
        {
            if (items.Count == 0) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item.Kind != YamlNodeKind.Map || item.Entries.Count != 1 || item.AsReference() != null) return false;
                if (!keys.Add(item.Entries[0].Key)) return false;
            }
            return true;
        }

        /// <summary>Lists every leaf value under <paramref name="node"/> with its path.</summary>
        private static void Flatten(string path, YamlNode node, Locator names, IGuidResolver resolver, Action<string, string> emit)
        {
            var reference = node.AsReference();
            if (reference != null)
            {
                emit(path, Describe(reference, names, resolver));
                return;
            }
            switch (node.Kind)
            {
                case YamlNodeKind.Map:
                    foreach (var entry in node.Entries)
                    {
                        if (!IgnoredFields.Contains(entry.Key)) Flatten(Join(path, entry.Key), entry.Value, names, resolver, emit);
                    }
                    break;
                case YamlNodeKind.Sequence:
                    if (node.Items.Count == 0)
                    {
                        emit(path, "[]");
                        break;
                    }
                    if (IsKeyedList(node.Items))
                    {
                        foreach (var item in node.Items) Flatten(Join(path, item.Entries[0].Key), item.Entries[0].Value, names, resolver, emit);
                    }
                    else
                    {
                        for (var i = 0; i < node.Items.Count; i++) Flatten(Index(path, i), node.Items[i], names, resolver, emit);
                    }
                    break;
                default:
                    emit(path, node.Value);
                    break;
            }
        }

        private static bool SameReference(UnityObjectReference a, UnityObjectReference b)
        {
            if (a == null || b == null) return false;
            return a.FileId == b.FileId && string.Equals(a.Guid, b.Guid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A reference as people read it: "None", an object in this file, or an asset path.</summary>
        internal static string Describe(UnityObjectReference reference, Locator names, IGuidResolver resolver)
        {
            if (reference.IsNull) return "None";
            if (reference.Guid == null) return names?.LocateById(reference.FileId) ?? ("#" + reference.FileId.ToString(CultureInfo.InvariantCulture));
            if (reference.Guid == BuiltinGuid || reference.Guid == BuiltinExtraGuid) return "Built-in (" + reference.FileId.ToString(CultureInfo.InvariantCulture) + ")";
            var resolved = resolver?.Resolve(reference.Guid);
            return string.IsNullOrEmpty(resolved) ? "guid:" + reference.Guid : resolved;
        }

        private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

        private static string Index(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

        /// <summary>Names objects of one file version: GameObject hierarchy paths through Transform parents.</summary>
        internal sealed class Locator
        {
            private readonly Dictionary<long, UnityYamlDocument> _byId = new Dictionary<long, UnityYamlDocument>();

            public Locator(IReadOnlyList<UnityYamlDocument> docs)
            {
                foreach (var doc in docs) _byId[doc.FileId] = doc;
            }

            public string Locate(UnityYamlDocument doc)
            {
                if (doc.TypeName == "GameObject") return GameObjectPath(doc, 0);
                var owner = doc.Body["m_GameObject"]?.AsReference();
                if (owner != null && !owner.IsNull && owner.Guid == null && _byId.TryGetValue(owner.FileId, out var go) && go.TypeName == "GameObject")
                {
                    return GameObjectPath(go, 0);
                }
                var name = doc.Body["m_Name"];
                return name != null && name.Kind == YamlNodeKind.Scalar ? name.Value : string.Empty;
            }

            public string LocateById(long fileId)
            {
                if (!_byId.TryGetValue(fileId, out var doc)) return null;
                var location = Locate(doc);
                return location.Length == 0 ? doc.TypeName : doc.TypeName + " (" + location + ")";
            }

            private string GameObjectPath(UnityYamlDocument go, int depth)
            {
                var name = go.Body["m_Name"]?.Value ?? string.Empty;
                if (depth > 64) return name; // a broken file must not loop forever
                var transform = FindTransform(go);
                var father = transform?.Body["m_Father"]?.AsReference();
                if (father == null || father.IsNull || father.Guid != null || !_byId.TryGetValue(father.FileId, out var parentTransform)) return name;
                var parentGo = parentTransform.Body["m_GameObject"]?.AsReference();
                if (parentGo == null || !_byId.TryGetValue(parentGo.FileId, out var parent)) return name;
                return GameObjectPath(parent, depth + 1) + "/" + name;
            }

            private UnityYamlDocument FindTransform(UnityYamlDocument go)
            {
                var components = go.Body["m_Component"];
                if (components == null) return null;
                foreach (var item in components.Items)
                {
                    var reference = item["component"]?.AsReference();
                    if (reference == null || !_byId.TryGetValue(reference.FileId, out var component)) continue;
                    if (component.TypeName == "Transform" || component.TypeName == "RectTransform") return component;
                }
                return null;
            }
        }
    }
}
