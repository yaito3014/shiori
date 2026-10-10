using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class UnityYamlTests
    {
        private const string Prefab = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 400}
  - component: {fileID: 2300}
  m_Layer: 0
  m_Name: Avatar
  m_IsActive: 1
--- !u!4 &400
Transform:
  m_GameObject: {fileID: 100}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_Children:
  - {fileID: 401}
  m_Father: {fileID: 0}
--- !u!1 &101
GameObject:
  m_Component:
  - component: {fileID: 401}
  m_Name: Hat
--- !u!4 &401
Transform:
  m_GameObject: {fileID: 101}
  m_LocalPosition: {x: 0, y: 1.5, z: 0}
  m_Children: []
  m_Father: {fileID: 400}
--- !u!23 &2300
MeshRenderer:
  m_GameObject: {fileID: 100}
  m_Enabled: 1
  m_Materials:
  - {fileID: 2100000, guid: aaaa1111aaaa1111aaaa1111aaaa1111, type: 2}
";

        private const string Material = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_Name: Skin
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {fileID: 2800000, guid: bbbb2222bbbb2222bbbb2222bbbb2222, type: 3}
        m_Scale: {x: 1, y: 1}
    m_Floats:
    - _Glossiness: 0.5
    - _Metallic: 0
    m_Colors:
    - _Color: {r: 1, g: 1, b: 1, a: 1}
";

        private sealed class Resolver : IGuidResolver
        {
            public string Resolve(string guid)
            {
                switch (guid)
                {
                    case "aaaa1111aaaa1111aaaa1111aaaa1111": return "Assets/Avatar/Skin.mat";
                    case "bbbb2222bbbb2222bbbb2222bbbb2222": return "Assets/Avatar/Skin.png";
                    case "cccc3333cccc3333cccc3333cccc3333": return "Assets/Avatar/Skin_Dark.png";
                    default: return null;
                }
            }
        }

        private static readonly Resolver Names = new Resolver();

        // ---- parser ----

        [Test]
        public void Parse_ReadsObjectHeadersAndBodies()
        {
            var docs = UnityYamlParser.Parse(Prefab);
            Assert.That(docs.Select(d => d.FileId), Is.EqualTo(new long[] { 100, 400, 101, 401, 2300 }));
            Assert.That(docs[0].ClassId, Is.EqualTo(1));
            Assert.That(docs[0].TypeName, Is.EqualTo("GameObject"));
            Assert.That(docs[0].Body["m_Name"].Value, Is.EqualTo("Avatar"));
            var components = docs[0].Body["m_Component"];
            Assert.That(components.Kind, Is.EqualTo(YamlNodeKind.Sequence), "a sequence written at its key's indent");
            Assert.That(components.Items.Count, Is.EqualTo(2));
            Assert.That(components.Items[1]["component"].AsReference().FileId, Is.EqualTo(2300));
            Assert.That(docs[1].Body["m_LocalRotation"]["w"].Value, Is.EqualTo("1"));
            Assert.That(docs[3].Body["m_Children"].Items, Is.Empty, "[] is an empty sequence");
        }

        [Test]
        public void Parse_ReadsStrippedHeadersAndNestedKeyedLists()
        {
            var docs = UnityYamlParser.Parse("--- !u!4 &7 stripped\nTransform:\n  m_PrefabInstance: {fileID: 9}\n" + Material.Substring(Material.IndexOf("--- ")));
            Assert.That(docs[0].Stripped, Is.True);
            var material = docs[1];
            Assert.That(material.TypeName, Is.EqualTo("Material"));
            var texEnvs = material.Body["m_SavedProperties"]["m_TexEnvs"];
            Assert.That(texEnvs.Items[0]["_MainTex"]["m_Texture"].AsReference().Guid, Is.EqualTo("bbbb2222bbbb2222bbbb2222bbbb2222"));
            Assert.That(material.Body["m_SavedProperties"]["m_Floats"].Items[1]["_Metallic"].Value, Is.EqualTo("0"));
        }

        [TestCase("\"a \\\"quoted\\\" \\u3042\"", "a \"quoted\" あ")]
        [TestCase("'it''s'", "it's")]
        [TestCase("plain text: with colon", "plain text: with colon")]
        [TestCase("{x: 1, y: {z: 'a,b'}}", "{x: 1, y: {z: a,b}}")]
        [TestCase("[1, two, {k: v}]", "[1, two, {k: v}]")]
        public void ParseInline_HandlesQuotesAndFlowCollections(string text, string expected)
        {
            Assert.That(UnityYamlParser.ParseInline(text).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void ParseBlock_FoldsWrappedStringsAndReadsBlockScalars()
        {
            var node = UnityYamlParser.ParseBlock("m_Text: \"a long string that\n    continues here\"\nm_Plain: first part\n  second part\nm_Literal: |\n  line one\n  line two\nm_After: 1");
            Assert.That(node["m_Text"].Value, Is.EqualTo("a long string that continues here"));
            Assert.That(node["m_Plain"].Value, Is.EqualTo("first part second part"));
            Assert.That(node["m_Literal"].Value, Is.EqualTo("line one\nline two"));
            Assert.That(node["m_After"].Value, Is.EqualTo("1"));
        }

        [Test]
        public void Parse_KeepsOddLinesAsRawInsteadOfFailing()
        {
            var docs = UnityYamlParser.Parse("--- !u!114 &5\nMonoBehaviour:\n  m_Name: x\n  this line is not yaml\n  m_Enabled: 1\n");
            Assert.That(docs.Single().Body["m_Enabled"].Value, Is.EqualTo("1"));
            Assert.That(docs.Single().Body["(raw)"].Value, Is.EqualTo("this line is not yaml"));
            Assert.That(UnityYamlParser.Parse(null), Is.Empty);
            Assert.That(UnityYamlParser.Parse("not unity yaml at all"), Is.Empty);
        }

        // ---- diff ----

        [Test]
        public void Diff_ReportsChangedPropertiesWithHierarchyPaths()
        {
            var changed = Prefab.Replace("m_LocalPosition: {x: 0, y: 1.5, z: 0}", "m_LocalPosition: {x: 0, y: 1.75, z: 0}")
                .Replace("m_IsActive: 1", "m_IsActive: 0");

            var changes = UnityYamlDiff.Compare(Prefab, changed, Names);

            Assert.That(changes.Count, Is.EqualTo(2));
            var avatar = changes.Single(c => c.FileId == 100);
            Assert.That(avatar.Kind, Is.EqualTo(UnityObjectChangeKind.Changed));
            Assert.That(avatar.Location, Is.EqualTo("Avatar"));
            Assert.That(avatar.Properties.Single().Path, Is.EqualTo("m_IsActive"));
            var hat = changes.Single(c => c.FileId == 401);
            Assert.That(hat.TypeName, Is.EqualTo("Transform"));
            Assert.That(hat.Location, Is.EqualTo("Avatar/Hat"), "path through Transform parents");
            Assert.That(hat.Properties.Single().Path, Is.EqualTo("m_LocalPosition.y"));
            Assert.That(hat.Properties.Single().OldValue, Is.EqualTo("1.5"));
            Assert.That(hat.Properties.Single().NewValue, Is.EqualTo("1.75"));
        }

        [Test]
        public void Diff_ReportsAddedAndRemovedObjects()
        {
            var withLight = Prefab + "--- !u!108 &10800\nLight:\n  m_GameObject: {fileID: 101}\n  m_Intensity: 2\n";
            var added = UnityYamlDiff.Compare(Prefab, withLight, Names).Single();
            Assert.That(added.Kind, Is.EqualTo(UnityObjectChangeKind.Added));
            Assert.That(added.TypeName, Is.EqualTo("Light"));
            Assert.That(added.Location, Is.EqualTo("Avatar/Hat"));
            Assert.That(added.Properties.Select(p => p.Path), Is.EqualTo(new[] { "m_GameObject", "m_Intensity" }));
            Assert.That(added.Properties[0].NewValue, Is.EqualTo("GameObject (Avatar/Hat)"), "references inside the file name the object");

            var removed = UnityYamlDiff.Compare(withLight, Prefab, Names).Single();
            Assert.That(removed.Kind, Is.EqualTo(UnityObjectChangeKind.Removed));
            Assert.That(removed.Properties[1].OldValue, Is.EqualTo("2"));
            Assert.That(UnityYamlDiff.Compare(null, Prefab, Names).Count, Is.EqualTo(5), "a new file is all additions");
        }

        [Test]
        public void Diff_MaterialPropertiesUseTheirNames_AndReferencesTheirAssets()
        {
            var changed = Material.Replace("_Color: {r: 1, g: 1, b: 1, a: 1}", "_Color: {r: 1, g: 0.5, b: 1, a: 1}")
                .Replace("bbbb2222bbbb2222bbbb2222bbbb2222", "cccc3333cccc3333cccc3333cccc3333")
                .Replace("    - _Metallic: 0\n", "")
                .Replace("serializedVersion: 8", "serializedVersion: 9");

            var change = UnityYamlDiff.Compare(Material, changed, Names).Single();

            Assert.That(change.Location, Is.EqualTo("Skin"));
            var byPath = change.Properties.ToDictionary(p => p.Path);
            Assert.That(byPath.Keys, Is.EquivalentTo(new[]
            {
                "m_SavedProperties.m_TexEnvs._MainTex.m_Texture",
                "m_SavedProperties.m_Floats._Metallic",
                "m_SavedProperties.m_Colors._Color.g",
            }), "serializedVersion is noise and is not reported");
            Assert.That(byPath["m_SavedProperties.m_TexEnvs._MainTex.m_Texture"].OldValue, Is.EqualTo("Assets/Avatar/Skin.png"));
            Assert.That(byPath["m_SavedProperties.m_TexEnvs._MainTex.m_Texture"].NewValue, Is.EqualTo("Assets/Avatar/Skin_Dark.png"));
            Assert.That(byPath["m_SavedProperties.m_Floats._Metallic"].NewValue, Is.Null, "removed property");
        }

        [Test]
        public void Diff_DescribesBuiltinNullAndUnknownReferences()
        {
            Assert.That(UnityYamlDiff.Describe(new UnityObjectReference(0, null), null, Names), Is.EqualTo("None"));
            Assert.That(UnityYamlDiff.Describe(new UnityObjectReference(46, UnityYamlDiff.BuiltinExtraGuid), null, Names), Is.EqualTo("Built-in (46)"));
            Assert.That(UnityYamlDiff.Describe(new UnityObjectReference(1, "dddd"), null, Names), Is.EqualTo("guid:dddd"));
            Assert.That(UnityYamlDiff.Describe(new UnityObjectReference(1, "dddd"), null, null), Is.EqualTo("guid:dddd"), "works without a resolver");
        }

        [Test]
        public void Diff_IdenticalFilesHaveNoChanges()
        {
            Assert.That(UnityYamlDiff.Compare(Prefab, Prefab, Names), Is.Empty);
            Assert.That(UnityYamlDiff.Compare(Prefab.Replace("\n", "\r\n"), Prefab, Names), Is.Empty, "line endings do not matter");
        }
    }
}
