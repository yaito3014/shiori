using System;
using System.Collections.Generic;
using System.Text;

namespace Shiori
{
    public enum YamlNodeKind
    {
        Scalar,
        Map,
        Sequence,
    }

    /// <summary>
    /// A node of the YAML subset Unity writes. Maps keep their key order (Unity's order is meaningful
    /// to people reading a diff). Scalars keep their text as written, minus quotes.
    /// </summary>
    public sealed class YamlNode
    {
        public YamlNodeKind Kind { get; }

        /// <summary>For scalars: the value text. Empty for maps and sequences.</summary>
        public string Value { get; }

        /// <summary>For maps: entries in file order. Empty otherwise.</summary>
        public IReadOnlyList<KeyValuePair<string, YamlNode>> Entries { get; }

        /// <summary>For sequences: items in file order. Empty otherwise.</summary>
        public IReadOnlyList<YamlNode> Items { get; }

        private YamlNode(YamlNodeKind kind, string value, IReadOnlyList<KeyValuePair<string, YamlNode>> entries, IReadOnlyList<YamlNode> items)
        {
            Kind = kind;
            Value = value ?? string.Empty;
            Entries = entries ?? Array.Empty<KeyValuePair<string, YamlNode>>();
            Items = items ?? Array.Empty<YamlNode>();
        }

        public static YamlNode Scalar(string value) => new YamlNode(YamlNodeKind.Scalar, value, null, null);

        public static YamlNode Map(IReadOnlyList<KeyValuePair<string, YamlNode>> entries) => new YamlNode(YamlNodeKind.Map, null, entries, null);

        public static YamlNode Sequence(IReadOnlyList<YamlNode> items) => new YamlNode(YamlNodeKind.Sequence, null, null, items);

        public static readonly YamlNode Empty = Scalar(string.Empty);

        /// <summary>The value of <paramref name="key"/> in a map, or null.</summary>
        public YamlNode this[string key]
        {
            get
            {
                foreach (var entry in Entries)
                {
                    if (string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry.Value;
                }
                return null;
            }
        }

        /// <summary>
        /// A Unity object reference <c>{fileID: …, guid: …, type: …}</c>, or null when this node is not one.
        /// </summary>
        public UnityObjectReference AsReference()
        {
            if (Kind != YamlNodeKind.Map) return null;
            var fileId = this["fileID"];
            if (fileId == null || fileId.Kind != YamlNodeKind.Scalar) return null;
            if (!long.TryParse(fileId.Value, out var id)) return null;
            var guid = this["guid"];
            return new UnityObjectReference(id, guid?.Kind == YamlNodeKind.Scalar ? guid.Value : null);
        }

        /// <summary>Compact one-line text, used when a whole value is shown (for example an added sequence item).</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }

        private void Write(StringBuilder sb)
        {
            switch (Kind)
            {
                case YamlNodeKind.Scalar:
                    sb.Append(Value);
                    break;
                case YamlNodeKind.Map:
                    sb.Append('{');
                    for (var i = 0; i < Entries.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(Entries[i].Key).Append(": ");
                        Entries[i].Value.Write(sb);
                    }
                    sb.Append('}');
                    break;
                default:
                    sb.Append('[');
                    for (var i = 0; i < Items.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        Items[i].Write(sb);
                    }
                    sb.Append(']');
                    break;
            }
        }
    }

    /// <summary>A reference to another object: in the same file (no guid) or in another asset (guid).</summary>
    public sealed class UnityObjectReference
    {
        public long FileId { get; }

        /// <summary>The referenced asset's GUID, or null for a reference inside the same file.</summary>
        public string Guid { get; }

        public bool IsNull => FileId == 0 && string.IsNullOrEmpty(Guid);

        public UnityObjectReference(long fileId, string guid)
        {
            FileId = fileId;
            Guid = string.IsNullOrEmpty(guid) ? null : guid;
        }
    }

    /// <summary>One object of a Unity YAML file: <c>--- !u!&lt;classId&gt; &amp;&lt;fileId&gt;</c> followed by <c>TypeName:</c> and its fields.</summary>
    public sealed class UnityYamlDocument
    {
        /// <summary>Unity's class ID (1 = GameObject, 4 = Transform, 21 = Material, 114 = MonoBehaviour, …).</summary>
        public int ClassId { get; }

        /// <summary>The object's local identifier in the file; stable across saves, so it matches objects between versions.</summary>
        public long FileId { get; }

        /// <summary>True for "stripped" placeholders of objects that live in a prefab asset.</summary>
        public bool Stripped { get; }

        /// <summary>The root key, for example GameObject, Transform, Material, MonoBehaviour.</summary>
        public string TypeName { get; }

        /// <summary>The object's fields (the map under <see cref="TypeName"/>).</summary>
        public YamlNode Body { get; }

        public UnityYamlDocument(int classId, long fileId, bool stripped, string typeName, YamlNode body)
        {
            ClassId = classId;
            FileId = fileId;
            Stripped = stripped;
            TypeName = typeName ?? string.Empty;
            Body = body ?? YamlNode.Map(null);
        }
    }
}
