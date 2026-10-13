using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shiori.Editor.Tests
{
    /// <summary>The conflict panel, the merge setup step, and UnityYAMLMerge itself as git's merge driver.</summary>
    public class MergeTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private string _root;
        private GitRepository _repo;
        private string _main;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-merge-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(_root)) return;
            foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if ((info.Attributes & FileAttributes.ReadOnly) != 0) info.Attributes &= ~FileAttributes.ReadOnly;
            }
            Directory.Delete(_root, true);
        }

        private static IEnumerator Await(Task task)
        {
            var started = System.DateTime.UtcNow;
            while (!task.IsCompleted)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("task did not finish within 60 seconds");
                yield return null;
            }
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion), task.Exception?.ToString());
        }

        /// <summary>Core calls do not need the main thread, so they can be waited for directly.</summary>
        private void InitRepository()
        {
            var git = new GitLocator(new ProcessGitRunner()).LocateAsync(null, None).GetAwaiter().GetResult();
            Assume.That(git.Found && git.MeetsMinimumVersion, Is.True, "a supported git is required");
            _repo = new GitRepository(new ProcessGitRunner(), git.Path, _root);
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
        }

        private void Save(string file, string text)
        {
            var full = Path.Combine(_root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text);
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            _repo.CommitAsync("save " + file, None).GetAwaiter().GetResult();
        }

        private static string Prefab(int alphaX, int betaX)
        {
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
                   + GameObject(100, "Alpha", alphaX) + GameObject(200, "Beta", betaX);
        }

        private static string GameObject(int id, string name, int x)
        {
            return "--- !u!1 &" + id + "\nGameObject:\n  m_ObjectHideFlags: 0\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: " + (id + 1) + "}\n"
                   + "  m_Layer: 0\n  m_Name: " + name + "\n  m_IsActive: 1\n"
                   + "--- !u!4 &" + (id + 1) + "\nTransform:\n  m_ObjectHideFlags: 0\n  m_GameObject: {fileID: " + id + "}\n"
                   + "  m_LocalPosition: {x: " + x + ", y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Children: []\n  m_Father: {fileID: 0}\n";
        }

        private System.Collections.Generic.IReadOnlyList<ConflictEntry> MergeAfter(int otherAlpha, int otherBeta, int mainAlpha, int mainBeta)
        {
            _repo.CreateBranchAsync("other", None).GetAwaiter().GetResult();
            Save("Assets/A.prefab", Prefab(otherAlpha, otherBeta));
            _repo.SwitchBranchAsync(_main, false, None).GetAwaiter().GetResult();
            Save("Assets/A.prefab", Prefab(mainAlpha, mainBeta));
            return MergeRunner.StartAsync(_repo, "other", null, None).GetAwaiter().GetResult();
        }

        private void PrepareDriver()
        {
            Assume.That(File.Exists(MergeSetupStep.ToolPath), Is.True, "this editor ships UnityYAMLMerge");
            InitRepository();
            MergeDriverSetup.ApplyAsync(_repo, MergeSetupStep.ToolPath, None).GetAwaiter().GetResult();
            Save("Assets/A.prefab", Prefab(0, 0));
            _main = _repo.GetStatusAsync(None).GetAwaiter().GetResult().Branch;
        }

        [Test]
        public void UnityYamlMerge_CombinesEditsToDifferentObjects()
        {
            PrepareDriver();
            var conflicts = MergeAfter(otherAlpha: 5, otherBeta: 0, mainAlpha: 0, mainBeta: 7);

            Assert.That(conflicts, Is.Empty, "git's line merge would conflict here; the driver merges per object");
            var merged = File.ReadAllText(Path.Combine(_root, "Assets", "A.prefab"));
            Assert.That(merged, Does.Contain("m_LocalPosition: {x: 5,"));
            Assert.That(merged, Does.Contain("m_LocalPosition: {x: 7,"));
            Assert.That(MergeRunner.GetStateAsync(_repo, None).GetAwaiter().GetResult().InProgress, Is.False);
        }

        [Test]
        public void UnityYamlMerge_LeavesTheSameFieldToTheUser()
        {
            PrepareDriver();
            var conflicts = MergeAfter(otherAlpha: 9, otherBeta: 0, mainAlpha: 3, mainBeta: 0);

            Assert.That(conflicts.Select(c => c.Path), Is.EqualTo(new[] { "Assets/A.prefab" }));
            Assert.That(File.ReadAllText(Path.Combine(_root, "Assets", "A.prefab")), Is.EqualTo(Prefab(3, 0)), "ours, untouched by the tool");
        }

        [UnityTest]
        public IEnumerator SetupStep_OffersTheSetup_ThenIsDone()
        {
            Assume.That(File.Exists(MergeSetupStep.ToolPath), Is.True, "this editor ships UnityYAMLMerge");
            InitRepository();
            var step = new MergeSetupStep(() => _repo);

            var first = step.EvaluateAsync(None);
            yield return Await(first);
            Assert.That(first.Result.Done, Is.False);
            Assert.That(first.Result.Actions.Single().Label, Is.EqualTo(L10n.Tr("step.merge.apply")));

            yield return Await(first.Result.Actions[0].Run(None));
            var second = step.EvaluateAsync(None);
            yield return Await(second);
            Assert.That(second.Result.Done, Is.True);
            Assert.That(second.Result.Message, Is.EqualTo(L10n.Tr("step.merge.ok")));

            var moved = new MergeSetupStep(() => _repo, Path.Combine(_root, "elsewhere.exe"));
            var missing = moved.EvaluateAsync(None);
            yield return Await(missing);
            Assert.That(missing.Result.Done, Is.True, "without the tool there is nothing to set up, and the wizard must not stop");
        }

        [Test]
        public void CoreSteps_AreFoundWithTheExtensions()
        {
            Assert.That(ExtensionRegistry.Discover().OfType<CoreSetupSteps>().Count(), Is.EqualTo(1));
        }

        private static ConflictEntry Entry(string path, bool oursDeleted = false) =>
            new ConflictEntry(path, "b", oursDeleted ? null : "o", "t");

        [Test]
        public void ConflictPanel_FoldsMetaAndOffersBothSides()
        {
            var conflicts = new[] { Entry("Assets/A.prefab"), Entry("Assets/A.prefab.meta"), Entry("Assets/B.mat", oursDeleted: true) };
            var rows = ConflictPanel.Rows(conflicts);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Entries.Select(e => e.Path), Is.EqualTo(new[] { "Assets/A.prefab", "Assets/A.prefab.meta" }), "an asset and its .meta are decided together");

            var panel = new ConflictPanel(gitTerms: false);
            Assert.That(panel.ClassListContains("shiori-hidden"), Is.True, "no merge, no panel");

            panel.Render(new MergeState(true, conflicts));
            Assert.That(panel.ClassListContains("shiori-hidden"), Is.False);
            var items = panel.Query(className: "shiori-merge-item").ToList();
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].Query<Button>().ToList().Select(b => b.text), Is.EqualTo(new[] { L10n.Tr("merge.ours"), L10n.Tr("merge.theirs") }));
            Assert.That(items[1].Query<Label>().ToList().Any(l => l.text == L10n.Tr("merge.deleted.ours")), Is.True);

            var detail = new ConflictPanel(gitTerms: true);
            detail.Render(new MergeState(true, new ConflictEntry[0]));
            var finish = detail.Q<Button>(className: "shiori-merge-finish");
            Assert.That(finish.ClassListContains("shiori-hidden"), Is.False, "detail mode offers the commit once nothing is left");
        }
    }
}
