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

        /// <summary>How the items of one list are matched between versions, when position is not their identity.</summary>
        private sealed class ListIdentity
        {
            /// <summary>The item field that identifies it; null when each item is itself a reference.</summary>
            public string Field { get; }

            /// <summary>True when <see cref="Field"/> holds a reference rather than a name.</summary>
            public bool FieldIsReference { get; }

            /// <summary>The item field shown as its label instead of what the reference points at; null for that.</summary>
            public string LabelField { get; }

            /// <summary>True when a new order means something (layer priority, transition priority).</summary>
            public bool OrderMatters { get; }

            private ListIdentity(string field, bool fieldIsReference, string labelField, bool orderMatters)
            {
                Field = field;
                FieldIsReference = fieldIsReference;
                LabelField = labelField;
                OrderMatters = orderMatters;
            }

            public static ListIdentity ByName(string field, bool orderMatters) => new ListIdentity(field, false, null, orderMatters);

            public static ListIdentity ByReferenceIn(string field, bool orderMatters, string labelField = null) => new ListIdentity(field, true, labelField, orderMatters);

            public static ListIdentity ByItemReference(bool orderMatters) => new ListIdentity(null, true, null, orderMatters);
        }

        /// <summary>
        /// Comparison rules for one object type. Kept per type so that animator rules (hidden graph
        /// positions, lists matched by name) never change how prefabs and materials are compared.
        /// </summary>
        private sealed class TypeRules
        {
            public static readonly TypeRules Default = new TypeRules();

            /// <summary>Fields hidden at any depth of the object, on top of <see cref="IgnoredFields"/>.</summary>
            public HashSet<string> Ignored { get; } = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// List identities by the list's own field name, tried in order: the first one every item of both
            /// versions has decides how the list is matched.
            /// </summary>
            public Dictionary<string, ListIdentity[]> Lists { get; } = new Dictionary<string, ListIdentity[]>(StringComparer.Ordinal);

            /// <summary>Names for numeric enum values, by field name.</summary>
            public Dictionary<string, Dictionary<string, string>> ValueNames { get; } = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            public bool IsIgnored(string key) => IgnoredFields.Contains(key) || Ignored.Contains(key);

            public string NameValue(string key, string value)
            {
                return key != null && value != null && ValueNames.TryGetValue(key, out var names) && names.TryGetValue(value, out var name) ? name : value;
            }
        }

        // Values as UnityEngine.AnimatorControllerParameterType and UnityEditor.Animations.AnimatorConditionMode
        // declare them (read from the 6000.6 editor assemblies).
        private static readonly Dictionary<string, string> ParameterTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1"] = "Float",
            ["3"] = "Int",
            ["4"] = "Bool",
            ["9"] = "Trigger",
        };

        private static readonly Dictionary<string, string> ConditionModes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1"] = "If",
            ["2"] = "IfNot",
            ["3"] = "Greater",
            ["4"] = "Less",
            ["6"] = "Equals",
            ["7"] = "NotEqual",
        };

        private static readonly Dictionary<string, TypeRules> RulesByType = BuildRules();

        private static Dictionary<string, TypeRules> BuildRules()
        {
            var controller = new TypeRules();
            controller.Lists["m_AnimatorParameters"] = new[] { ListIdentity.ByName("m_Name", orderMatters: true) };
            // A layer keeps its state machine when renamed; synced layers have none and fall back to the name.
            controller.Lists["m_AnimatorLayers"] = new[]
            {
                ListIdentity.ByReferenceIn("m_StateMachine", orderMatters: true, labelField: "m_Name"),
                ListIdentity.ByName("m_Name", orderMatters: true),
            };
            controller.ValueNames["m_Type"] = ParameterTypes;

            // Node positions in the Animator window's graph: dragging a node is not a change to the animation.
            var machine = new TypeRules();
            foreach (var field in new[] { "m_Position", "m_AnyStatePosition", "m_EntryPosition", "m_ExitPosition", "m_ParentStateMachinePosition" }) machine.Ignored.Add(field);
            machine.Lists["m_ChildStates"] = new[] { ListIdentity.ByReferenceIn("m_State", orderMatters: false) };
            machine.Lists["m_ChildStateMachines"] = new[] { ListIdentity.ByReferenceIn("m_StateMachine", orderMatters: false) };
            machine.Lists["m_AnyStateTransitions"] = new[] { ListIdentity.ByItemReference(orderMatters: true) };
            machine.Lists["m_EntryTransitions"] = new[] { ListIdentity.ByItemReference(orderMatters: true) };
            machine.Lists["m_StateMachineBehaviours"] = new[] { ListIdentity.ByItemReference(orderMatters: true) };

            var state = new TypeRules();
            state.Ignored.Add("m_Position");
            state.Lists["m_Transitions"] = new[] { ListIdentity.ByItemReference(orderMatters: true) };
            state.Lists["m_StateMachineBehaviours"] = new[] { ListIdentity.ByItemReference(orderMatters: true) };

            var transition = new TypeRules();
            transition.ValueNames["m_ConditionMode"] = ConditionModes;

            return new Dictionary<string, TypeRules>(StringComparer.Ordinal)
            {
                ["AnimatorController"] = controller,
                ["AnimatorStateMachine"] = machine,
                ["AnimatorState"] = state,
                ["AnimatorStateTransition"] = transition,
                ["AnimatorTransition"] = transition,
            };
        }

        private static TypeRules RulesFor(UnityYamlDocument doc) => RulesByType.TryGetValue(doc.TypeName, out var rules) ? rules : TypeRules.Default;

        /// <summary>What one comparison needs besides the two nodes: the names of both versions and the object type's rules.</summary>
        private sealed class Context
        {
            public Locator OldNames { get; }
            public Locator NewNames { get; }
            public IGuidResolver Resolver { get; }
            public TypeRules Rules { get; }

            public Context(Locator oldNames, Locator newNames, IGuidResolver resolver, TypeRules rules)
            {
                OldNames = oldNames;
                NewNames = newNames;
                Resolver = resolver;
                Rules = rules;
            }
        }

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
                var context = new Context(oldNames, newNames, resolver, RulesFor(doc));
                if (!oldById.TryGetValue(doc.FileId, out var before))
                {
                    result.Add(new UnityObjectChange(UnityObjectChangeKind.Added, doc.FileId, doc.TypeName, newNames.Locate(doc),
                        AllProperties(doc.Body, newNames, context, added: true)));
                    continue;
                }
                var properties = new List<UnityPropertyChange>();
                CompareNode(string.Empty, null, before.Body, doc.Body, context, properties);
                if (properties.Count > 0)
                {
                    result.Add(new UnityObjectChange(UnityObjectChangeKind.Changed, doc.FileId, doc.TypeName, newNames.Locate(doc), properties));
                }
            }
            foreach (var doc in oldDocs)
            {
                if (newById.ContainsKey(doc.FileId)) continue;
                var context = new Context(oldNames, newNames, resolver, RulesFor(doc));
                result.Add(new UnityObjectChange(UnityObjectChangeKind.Removed, doc.FileId, doc.TypeName, oldNames.Locate(doc),
                    AllProperties(doc.Body, oldNames, context, added: false)));
            }
            return result;
        }

        private static Dictionary<long, UnityYamlDocument> Index(IReadOnlyList<UnityYamlDocument> docs)
        {
            var index = new Dictionary<long, UnityYamlDocument>();
            foreach (var doc in docs) index[doc.FileId] = doc;
            return index;
        }

        private static IReadOnlyList<UnityPropertyChange> AllProperties(YamlNode body, Locator names, Context context, bool added)
        {
            var list = new List<UnityPropertyChange>();
            Flatten(string.Empty, null, body, names, context, (path, value) => list.Add(added ? new UnityPropertyChange(path, null, value) : new UnityPropertyChange(path, value, null)));
            return list;
        }

        /// <param name="key">The map key <paramref name="path"/> ends in, or null for list items and the object itself.</param>
        private static void CompareNode(string path, string key, YamlNode before, YamlNode after, Context context, List<UnityPropertyChange> changes)
        {
            // References compare by what they point at, so a moved fileID in the same object is not reported twice.
            var oldRef = before.AsReference();
            var newRef = after.AsReference();
            if (oldRef != null || newRef != null)
            {
                var o = oldRef != null ? Describe(oldRef, context.OldNames, context.Resolver) : before.ToString();
                var n = newRef != null ? Describe(newRef, context.NewNames, context.Resolver) : after.ToString();
                if (!SameReference(oldRef, newRef) && o != n) changes.Add(new UnityPropertyChange(path, o, n));
                return;
            }

            if (before.Kind == YamlNodeKind.Map && after.Kind == YamlNodeKind.Map)
            {
                foreach (var entry in after.Entries)
                {
                    if (context.Rules.IsIgnored(entry.Key)) continue;
                    var child = Join(path, entry.Key);
                    var previous = before[entry.Key];
                    if (previous == null) Flatten(child, entry.Key, entry.Value, context.NewNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
                    else CompareNode(child, entry.Key, previous, entry.Value, context, changes);
                }
                foreach (var entry in before.Entries)
                {
                    if (context.Rules.IsIgnored(entry.Key) || after[entry.Key] != null) continue;
                    Flatten(Join(path, entry.Key), entry.Key, entry.Value, context.OldNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
                }
                return;
            }

            if (before.Kind == YamlNodeKind.Sequence && after.Kind == YamlNodeKind.Sequence)
            {
                CompareSequence(path, key, before.Items, after.Items, context, changes);
                return;
            }

            var oldText = before.Kind == YamlNodeKind.Scalar ? context.Rules.NameValue(key, before.Value) : before.ToString();
            var newText = after.Kind == YamlNodeKind.Scalar ? context.Rules.NameValue(key, after.Value) : after.ToString();
            if (!string.Equals(oldText, newText, StringComparison.Ordinal)) changes.Add(new UnityPropertyChange(path, oldText, newText));
        }

        /// <summary>
        /// Lists with an identity in the type's rules match items by it (animator parameters by name,
        /// transitions by what they are). Material property lists ("- _Color: {…}") are lists of one-key
        /// maps; matching those by key reads as "_Color.r" instead of "[3].r" and survives reordering.
        /// Other lists compare by index.
        /// </summary>
        private static void CompareSequence(string path, string key, IReadOnlyList<YamlNode> before, IReadOnlyList<YamlNode> after, Context context, List<UnityPropertyChange> changes)
        {
            if (key != null && context.Rules.Lists.TryGetValue(key, out var identities))
            {
                foreach (var identity in identities)
                {
                    var oldItems = IdentifyItems(before, identity, context.OldNames, context.Resolver);
                    var newItems = IdentifyItems(after, identity, context.NewNames, context.Resolver);
                    if (oldItems == null || newItems == null) continue;
                    CompareIdentifiedItems(path, identity, oldItems, newItems, context, changes);
                    return;
                }
            }

            if (IsKeyedList(before) && IsKeyedList(after))
            {
                var oldByKey = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
                foreach (var item in before) oldByKey[item.Entries[0].Key] = item.Entries[0].Value;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in after)
                {
                    var itemKey = item.Entries[0].Key;
                    seen.Add(itemKey);
                    var child = Join(path, itemKey);
                    if (oldByKey.TryGetValue(itemKey, out var previous)) CompareNode(child, itemKey, previous, item.Entries[0].Value, context, changes);
                    else Flatten(child, itemKey, item.Entries[0].Value, context.NewNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
                }
                foreach (var pair in oldByKey)
                {
                    if (!seen.Contains(pair.Key)) Flatten(Join(path, pair.Key), pair.Key, pair.Value, context.OldNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
                }
                return;
            }

            var common = Math.Min(before.Count, after.Count);
            for (var i = 0; i < common; i++) CompareNode(Index(path, i), null, before[i], after[i], context, changes);
            for (var i = common; i < after.Count; i++) Flatten(Index(path, i), null, after[i], context.NewNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
            for (var i = common; i < before.Count; i++) Flatten(Index(path, i), null, before[i], context.OldNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
        }

        /// <summary>One list item with the identity it is matched by and the label people see for it.</summary>
        private sealed class IdentifiedItem
        {
            public string Key { get; }
            public string Label { get; }
            public YamlNode Node { get; }

            public IdentifiedItem(string key, string label, YamlNode node)
            {
                Key = key;
                Label = label;
                Node = node;
            }
        }

        /// <summary>The items with their identities, or null when some item has none or two share one (then position is all there is).</summary>
        private static List<IdentifiedItem> IdentifyItems(IReadOnlyList<YamlNode> items, ListIdentity identity, Locator names, IGuidResolver resolver)
        {
            var result = new List<IdentifiedItem>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                string key, label;
                if (identity.FieldIsReference)
                {
                    var reference = identity.Field == null ? item.AsReference() : item[identity.Field]?.AsReference();
                    if (reference == null || reference.IsNull) return null;
                    key = reference.FileId.ToString(CultureInfo.InvariantCulture) + ":" + reference.Guid;
                    var labelNode = identity.LabelField == null ? null : item[identity.LabelField];
                    if (labelNode != null && labelNode.Kind == YamlNodeKind.Scalar) label = labelNode.Value;
                    else label = reference.Guid == null ? names.LocationById(reference.FileId) : null;
                    if (string.IsNullOrEmpty(label)) label = Describe(reference, names, resolver);
                }
                else
                {
                    var name = item[identity.Field];
                    if (name == null || name.Kind != YamlNodeKind.Scalar || name.Value.Length == 0) return null;
                    key = label = name.Value;
                }
                if (!keys.Add(key)) return null;
                result.Add(new IdentifiedItem(key, label, item));
            }
            return result;
        }

        private static void CompareIdentifiedItems(string path, ListIdentity identity, List<IdentifiedItem> before, List<IdentifiedItem> after, Context context, List<UnityPropertyChange> changes)
        {
            var oldByKey = new Dictionary<string, IdentifiedItem>(StringComparer.Ordinal);
            foreach (var item in before) oldByKey[item.Key] = item;
            var newKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in after) newKeys.Add(item.Key);

            if (identity.OrderMatters && !SameRelativeOrder(before, after, oldByKey, newKeys))
            {
                changes.Add(new UnityPropertyChange(path, LabelList(before), LabelList(after)));
            }

            foreach (var item in after)
            {
                var child = identity.Field == null ? path : path + "[" + item.Label + "]";
                if (oldByKey.TryGetValue(item.Key, out var previous))
                {
                    // An item that is only a reference is the same item when its key matches.
                    if (identity.Field != null) CompareNode(child, null, previous.Node, item.Node, context, changes);
                }
                else
                {
                    Flatten(child, null, item.Node, context.NewNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, null, v)));
                }
            }
            foreach (var item in before)
            {
                if (newKeys.Contains(item.Key)) continue;
                var child = identity.Field == null ? path : path + "[" + item.Label + "]";
                Flatten(child, null, item.Node, context.OldNames, context, (p, v) => changes.Add(new UnityPropertyChange(p, v, null)));
            }
        }

        /// <summary>True when the items both versions have come in the same order; additions and removals alone do not reorder.</summary>
        private static bool SameRelativeOrder(List<IdentifiedItem> before, List<IdentifiedItem> after, Dictionary<string, IdentifiedItem> oldByKey, HashSet<string> newKeys)
        {
            var oldOrder = new List<string>();
            foreach (var item in before)
            {
                if (newKeys.Contains(item.Key)) oldOrder.Add(item.Key);
            }
            var index = 0;
            foreach (var item in after)
            {
                if (!oldByKey.ContainsKey(item.Key)) continue;
                if (!string.Equals(oldOrder[index], item.Key, StringComparison.Ordinal)) return false;
                index++;
            }
            return true;
        }

        private static string LabelList(List<IdentifiedItem> items)
        {
            var labels = new string[items.Count];
            for (var i = 0; i < items.Count; i++) labels[i] = items[i].Label;
            return "[" + string.Join(", ", labels) + "]";
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
        /// <param name="key">The map key <paramref name="path"/> ends in, or null for list items and the object itself.</param>
        private static void Flatten(string path, string key, YamlNode node, Locator names, Context context, Action<string, string> emit)
        {
            var reference = node.AsReference();
            if (reference != null)
            {
                emit(path, Describe(reference, names, context.Resolver));
                return;
            }
            switch (node.Kind)
            {
                case YamlNodeKind.Map:
                    foreach (var entry in node.Entries)
                    {
                        if (!context.Rules.IsIgnored(entry.Key)) Flatten(Join(path, entry.Key), entry.Key, entry.Value, names, context, emit);
                    }
                    break;
                case YamlNodeKind.Sequence:
                    if (node.Items.Count == 0)
                    {
                        emit(path, "[]");
                        break;
                    }
                    if (key != null && context.Rules.Lists.TryGetValue(key, out var identities) && FlattenIdentified(path, node.Items, identities, names, context, emit)) break;
                    if (IsKeyedList(node.Items))
                    {
                        foreach (var item in node.Items) Flatten(Join(path, item.Entries[0].Key), item.Entries[0].Key, item.Entries[0].Value, names, context, emit);
                    }
                    else
                    {
                        for (var i = 0; i < node.Items.Count; i++) Flatten(Index(path, i), null, node.Items[i], names, context, emit);
                    }
                    break;
                default:
                    emit(path, context.Rules.NameValue(key, node.Value));
                    break;
            }
        }

        /// <summary>Flattens a list under the first identity all its items have; false when none fits.</summary>
        private static bool FlattenIdentified(string path, IReadOnlyList<YamlNode> nodes, ListIdentity[] identities, Locator names, Context context, Action<string, string> emit)
        {
            foreach (var identity in identities)
            {
                var items = IdentifyItems(nodes, identity, names, context.Resolver);
                if (items == null) continue;
                foreach (var item in items) Flatten(identity.Field == null ? path : path + "[" + item.Label + "]", null, item.Node, names, context, emit);
                return true;
            }
            return false;
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

        /// <summary>
        /// Names objects of one file version: GameObject hierarchy paths through Transform parents, and in
        /// animator controllers "state machine/state" for states and "source → destination" for transitions.
        /// </summary>
        internal sealed class Locator
        {
            private const int MaxDepth = 64; // a broken file must not loop forever

            private readonly Dictionary<long, UnityYamlDocument> _byId = new Dictionary<long, UnityYamlDocument>();

            /// <summary>The state machine each state or child state machine sits in.</summary>
            private readonly Dictionary<long, long> _machineOf = new Dictionary<long, long>();

            /// <summary>
            /// Where each transition starts: a state or (leaving it) a child state machine with no label, or a
            /// state machine with "Any State" / "Entry".
            /// </summary>
            private readonly Dictionary<long, KeyValuePair<long, string>> _transitionSource = new Dictionary<long, KeyValuePair<long, string>>();

            /// <summary>The state or state machine each state machine behaviour belongs to.</summary>
            private readonly Dictionary<long, long> _behaviourOwner = new Dictionary<long, long>();

            public Locator(IReadOnlyList<UnityYamlDocument> docs)
            {
                foreach (var doc in docs) _byId[doc.FileId] = doc;
                foreach (var doc in docs)
                {
                    if (doc.TypeName == "AnimatorStateMachine")
                    {
                        foreach (var item in Items(doc.Body["m_ChildStates"])) Record(_machineOf, item["m_State"], doc.FileId);
                        foreach (var item in Items(doc.Body["m_ChildStateMachines"])) Record(_machineOf, item["m_StateMachine"], doc.FileId);
                        foreach (var item in Items(doc.Body["m_AnyStateTransitions"])) RecordSource(item, doc.FileId, "Any State");
                        foreach (var item in Items(doc.Body["m_EntryTransitions"])) RecordSource(item, doc.FileId, "Entry");
                        // "- first: <child machine>  second: [transitions]": transitions leaving that child machine.
                        foreach (var pair in Items(doc.Body["m_StateMachineTransitions"]))
                        {
                            var child = LocalId(pair["first"]);
                            if (!child.HasValue) continue;
                            foreach (var item in Items(pair["second"])) RecordSource(item, child.Value, null);
                        }
                        foreach (var item in Items(doc.Body["m_StateMachineBehaviours"])) Record(_behaviourOwner, item, doc.FileId);
                    }
                    else if (doc.TypeName == "AnimatorState")
                    {
                        foreach (var item in Items(doc.Body["m_Transitions"])) RecordSource(item, doc.FileId, null);
                        foreach (var item in Items(doc.Body["m_StateMachineBehaviours"])) Record(_behaviourOwner, item, doc.FileId);
                    }
                }
            }

            private static IReadOnlyList<YamlNode> Items(YamlNode list) => list != null && list.Kind == YamlNodeKind.Sequence ? list.Items : Array.Empty<YamlNode>();

            private static long? LocalId(YamlNode node)
            {
                var reference = node?.AsReference();
                return reference == null || reference.IsNull || reference.Guid != null ? (long?)null : reference.FileId;
            }

            private static void Record(Dictionary<long, long> map, YamlNode reference, long owner)
            {
                var id = LocalId(reference);
                if (id.HasValue && !map.ContainsKey(id.Value)) map[id.Value] = owner;
            }

            private void RecordSource(YamlNode reference, long owner, string pseudoState)
            {
                var id = LocalId(reference);
                if (id.HasValue && !_transitionSource.ContainsKey(id.Value)) _transitionSource[id.Value] = new KeyValuePair<long, string>(owner, pseudoState);
            }

            public string Locate(UnityYamlDocument doc) => Locate(doc, 0);

            private string Locate(UnityYamlDocument doc, int depth)
            {
                if (depth > MaxDepth) return NameOf(doc);
                switch (doc.TypeName)
                {
                    case "GameObject":
                        return GameObjectPath(doc, 0);
                    case "AnimatorStateMachine":
                        return MachinePath(doc.FileId, depth);
                    case "AnimatorState":
                        return _machineOf.TryGetValue(doc.FileId, out var machine) ? MachinePath(machine, depth + 1) + "/" + NameOf(doc) : NameOf(doc);
                    case "AnimatorStateTransition":
                    case "AnimatorTransition":
                        return TransitionPath(doc, depth);
                }
                var owner = doc.Body["m_GameObject"]?.AsReference();
                if (owner != null && !owner.IsNull && owner.Guid == null && _byId.TryGetValue(owner.FileId, out var go) && go.TypeName == "GameObject")
                {
                    return GameObjectPath(go, 0);
                }
                if (_behaviourOwner.TryGetValue(doc.FileId, out var behaviourOwner) && _byId.TryGetValue(behaviourOwner, out var ownerDoc))
                {
                    return Locate(ownerDoc, depth + 1);
                }
                return NameOf(doc);
            }

            private static string NameOf(UnityYamlDocument doc)
            {
                var name = doc.Body["m_Name"];
                return name != null && name.Kind == YamlNodeKind.Scalar ? name.Value : string.Empty;
            }

            /// <summary>"Base Layer/Sub" for a state machine nested in the layer's root machine.</summary>
            private string MachinePath(long fileId, int depth)
            {
                if (!_byId.TryGetValue(fileId, out var doc)) return string.Empty;
                var name = NameOf(doc);
                if (depth > MaxDepth || !_machineOf.TryGetValue(fileId, out var parent)) return name;
                return MachinePath(parent, depth + 1) + "/" + name;
            }

            private string TransitionPath(UnityYamlDocument doc, int depth)
            {
                var source = string.Empty;
                if (_transitionSource.TryGetValue(doc.FileId, out var from))
                {
                    if (from.Value != null) source = MachinePath(from.Key, depth + 1) + "/" + from.Value;
                    else if (_byId.TryGetValue(from.Key, out var state)) source = Locate(state, depth + 1);
                }

                var destination = string.Empty;
                var dstState = LocalId(doc.Body["m_DstState"]);
                var dstMachine = LocalId(doc.Body["m_DstStateMachine"]);
                if (dstState.HasValue && _byId.TryGetValue(dstState.Value, out var target)) destination = Locate(target, depth + 1);
                else if (dstMachine.HasValue) destination = MachinePath(dstMachine.Value, depth + 1);
                else if (doc.Body["m_IsExit"]?.Value == "1") destination = "Exit";

                if (destination.Length == 0) return source;
                return source.Length == 0 ? "→ " + destination : source + " → " + destination;
            }

            /// <summary>The location of the object with <paramref name="fileId"/> in this file, or null when there is none.</summary>
            public string LocationById(long fileId) => _byId.TryGetValue(fileId, out var doc) ? Locate(doc) : null;

            public string LocateById(long fileId)
            {
                if (!_byId.TryGetValue(fileId, out var doc)) return null;
                var location = Locate(doc);
                return location.Length == 0 ? doc.TypeName : doc.TypeName + " (" + location + ")";
            }

            private string GameObjectPath(UnityYamlDocument go, int depth)
            {
                var name = go.Body["m_Name"]?.Value ?? string.Empty;
                if (depth > MaxDepth) return name;
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
