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
    /// <summary>Every main view must build without throwing; a constructor failure leaves the window empty.</summary>
    public class ViewConstructionTests
    {
        private string _root;
        private ShioriSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-views-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
            _session = new ShioriSession(_root);
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

        private IEnumerator PrepareRepository()
        {
            yield return Await(_session.LocateGitAsync(CancellationToken.None));
            Assume.That(_session.Repository, Is.Not.Null, "a supported git is required");
            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "v1");
            yield return Await(_session.FirstSaveAsync("Shiori Test", "shiori@example.com", CancellationToken.None));
            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "v2\n");
            File.WriteAllText(Path.Combine(_root, "Assets", "new.txt"), "new\n");
        }

        [UnityTest]
        public IEnumerator DetailModeView_BuildsAndLoadsStatusAndDiff()
        {
            yield return PrepareRepository();

            var view = new DetailModeView(_session);
            Assert.That(view.Q<ListView>("files-list"), Is.Not.Null);
            Assert.That(view.Q<TwoPaneSplitView>("split"), Is.Not.Null);

            view.RestoreSelection("Assets/a.txt");
            view.RefreshAll();
            var started = System.DateTime.UtcNow;
            while (view.SelectedPath == null || view.Q<ListView>("diff-lines").itemsSource.Count == 0)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("status / diff did not load");
                yield return null;
            }

            Assert.That(view.Q<ListView>("files-list").itemsSource.Count, Is.EqualTo(2));
            Assert.That(view.SelectedPath, Is.EqualTo("Assets/a.txt"));
            Assert.That(view.Q<ListView>("diff-lines").itemsSource.Count, Is.GreaterThan(0));
            Assert.That(view.Q<Label>("view-error").ClassListContains("shiori-hidden"), Is.True, view.Q<Label>("view-error").text);
        }

        [UnityTest]
        public IEnumerator DetailModeView_ListsCommitsAndStashes_AndRemembersTheTab()
        {
            yield return PrepareRepository();
            // A stash to list: set the current edits aside the way 「保存せずに戻す」 does.
            var head = _session.Repository.GetHeadAsync(CancellationToken.None);
            yield return Await(head);
            yield return Await(RestoreRunner.RunAsync(_session.Repository, head.Result, "first", RestoreMode.StashFirst, null, CancellationToken.None));

            var view = new DetailModeView(_session);
            Assert.That(view.CurrentTab, Is.EqualTo(DetailModeView.TabChanges));
            view.RefreshAll();
            var started = System.DateTime.UtcNow;
            while (view.Q<ListView>("log-list").itemsSource.Count == 0 || view.Q<ScrollView>("stash-list").childCount == 0)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("log / stashes did not load");
                yield return null;
            }
            Assert.That(view.Q<ScrollView>("stash-list").childCount, Is.EqualTo(1));
            Assert.That(view.Q<Label>("remote-status").text, Is.EqualTo(L10n.Tr("detail.remote.none")));
            Assert.That(view.Q<Button>("push-button").text, Is.EqualTo(L10n.Tr("detail.remote.setup")));
            Assert.That(view.Q<Button>("pull-button").ClassListContains("shiori-hidden"), Is.True);

            view.ShowTab(DetailModeView.TabHistory);
            Assert.That(view.Q<VisualElement>("page-history").ClassListContains("shiori-hidden"), Is.False);
            Assert.That(view.Q<VisualElement>("page-changes").ClassListContains("shiori-hidden"), Is.True);
            Assert.That(new ShioriSession(_root).User.LastTab, Is.EqualTo(DetailModeView.TabHistory), "the tab is remembered in UserSettings");
            Assert.That(new DetailModeView(_session).CurrentTab, Is.EqualTo(DetailModeView.TabHistory), "and reopened");
        }

        private const string MaterialText = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  m_Name: Skin
  m_SavedProperties:
    m_Colors:
    - _Color: {r: 1, g: 1, b: 1, a: 1}
";

        [UnityTest]
        public IEnumerator DetailModeView_ShowsAMaterialChangeInTheUnityView()
        {
            yield return PrepareRepository();
            File.WriteAllText(Path.Combine(_root, "Assets", "Skin.mat"), MaterialText);
            yield return Await(SaveFlow.RunAsync(_session, "add material", CancellationToken.None));
            File.WriteAllText(Path.Combine(_root, "Assets", "Skin.mat"), MaterialText.Replace("g: 1,", "g: 0.5,"));

            var view = new DetailModeView(_session);
            view.ShowTab(DetailModeView.TabChanges);
            view.RestoreSelection("Assets/Skin.mat");
            view.RefreshAll();
            var unity = view.Q<ListView>("unity-diff");
            var started = System.DateTime.UtcNow;
            while (unity.ClassListContains("shiori-hidden") || unity.itemsSource.Count < 2)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("the Unity view did not appear");
                yield return null;
            }
            Assert.That(view.Q<VisualElement>("diff-mode").ClassListContains("shiori-hidden"), Is.False, "the switch is shown for .mat");
            Assert.That(view.Q<ListView>("diff-lines").ClassListContains("shiori-hidden"), Is.True, "the text diff steps aside");
            var rows = (System.Collections.Generic.List<UnityDiffRow>)unity.itemsSource;
            Assert.That(rows[0].Text, Is.EqualTo(L10n.Tr("detail.unity.changed") + "  Material (Skin)"));
            Assert.That(rows[1].Text, Is.EqualTo("m_SavedProperties.m_Colors._Color.g: 1  →  0.5"));
        }

        [Test]
        public void UnityDiffRows_DescribeEachKind()
        {
            Assert.That(UnityDiffPresenter.IsSupported("Assets/a.prefab"), Is.True);
            Assert.That(UnityDiffPresenter.IsSupported("Assets/a.MAT"), Is.True);
            Assert.That(UnityDiffPresenter.IsSupported("Assets/a.unity"), Is.False, "scenes are not in the first version");
            Assert.That(UnityDiffPresenter.IsSupported(null), Is.False);

            var none = UnityDiffPresenter.BuildRows(new UnityObjectChange[0]);
            Assert.That(none.Single().Text, Is.EqualTo(L10n.Tr("detail.unity.none")));

            var rows = UnityDiffPresenter.BuildRows(new[]
            {
                new UnityObjectChange(UnityObjectChangeKind.Added, 1, "Light", "Avatar/Hat", new[] { new UnityPropertyChange("m_Intensity", null, "2") }),
                new UnityObjectChange(UnityObjectChangeKind.Removed, 2, "Material", "", new[] { new UnityPropertyChange("m_Name", "Old", null) }),
            });
            Assert.That(rows.Select(r => r.Text), Is.EqualTo(new[]
            {
                L10n.Tr("detail.unity.added") + "  Light (Avatar/Hat)",
                "m_Intensity: 2",
                L10n.Tr("detail.unity.removed") + "  Material",
                "m_Name: Old",
            }));
            Assert.That(rows[1].Change, Is.EqualTo(UnityObjectChangeKind.Added));
            Assert.That(rows[3].Change, Is.EqualTo(UnityObjectChangeKind.Removed));
        }

        [UnityTest]
        public IEnumerator SaveFlow_CommitsEverythingAndReportsNothingToSave()
        {
            yield return PrepareRepository();
            var save = SaveFlow.RunAsync(_session, "detail commit", CancellationToken.None);
            yield return Await(save);
            Assert.That(save.Result, Is.Not.Null);
            var log = _session.Repository.GetLogAsync(1, 0, CancellationToken.None);
            yield return Await(log);
            Assert.That(log.Result[0].Message, Is.EqualTo("detail commit"));

            var again = SaveFlow.RunAsync(_session, "nothing", CancellationToken.None);
            yield return Await(again);
            Assert.That(again.Result, Is.Null, "a clean tree commits nothing");
        }

        [Test]
        public void DetailMessages_ExplainEachOutcome()
        {
            Assert.That(DetailModeView.DescribeRemote(new SendStatus("u", 2, false), new RemoteComparison(true, 2, 1)), Is.EqualTo(L10n.Tr("detail.remote.diverged", 2, 1)));
            Assert.That(DetailModeView.DescribeRemote(new SendStatus("u", 0, false), new RemoteComparison(true, 0, 3)), Is.EqualTo(L10n.Tr("detail.remote.status", 0, 3)));
            Assert.That(DetailModeView.DescribeRemote(new SendStatus("u", 4, true), RemoteComparison.Unknown), Is.EqualTo(L10n.Tr("detail.remote.unknown")));
            Assert.That(DetailModeView.DescribeRemote(null, null), Is.EqualTo(L10n.Tr("detail.remote.none")));
            Assert.That(DetailModeView.DescribePush(new SendResult(SendOutcome.Sent, 3, false)), Is.EqualTo(L10n.Tr("detail.push.done", 3)));
            Assert.That(DetailModeView.DescribePull(new ReceiveResult(ReceiveOutcome.Diverged, 2)), Is.EqualTo(L10n.Tr("detail.pull.diverged", 2)));
            Assert.That(DetailModeView.DescribeStashApply(new SetAsidePlan(SetAsideBlock.None, new[] { "a", "b" }, null)), Is.EqualTo(L10n.Tr("detail.stash.done", 2)));
            Assert.That(DetailModeView.DescribeStashApply(new SetAsidePlan(SetAsideBlock.Overlap, new[] { "a" }, new[] { "a" })), Is.EqualTo(L10n.Tr("detail.stash.overlap", "a")));
            foreach (ReceiveOutcome outcome in System.Enum.GetValues(typeof(ReceiveOutcome)))
            {
                Assert.That(DetailModeView.DescribePull(new ReceiveResult(outcome)), Does.Not.StartWith("detail."), outcome.ToString());
            }
            foreach (SendOutcome outcome in System.Enum.GetValues(typeof(SendOutcome)))
            {
                Assert.That(DetailModeView.DescribePush(new SendResult(outcome, 0, false)), Does.Not.StartWith("detail."), outcome.ToString());
            }
        }

        [Test]
        public void DetailModeView_FindsTheEditorsMonospaceFont()
        {
            Assert.That(DetailModeView.Monospace, Is.Not.Null, "none of these paths loaded: " + string.Join(", ", DetailModeView.MonospaceFontPaths));
        }

        [UnityTest]
        public IEnumerator SimpleModeView_BuildsAndLoadsHistory()
        {
            yield return PrepareRepository();

            var view = new SimpleModeView(_session);
            view.RefreshAll();
            var pending = view.Q<ListView>("save-changes");
            var started = System.DateTime.UtcNow;
            // Both the history and the pending-change list are rendered once status and log have loaded.
            while (view.Q<ListView>("history-list").itemsSource.Count == 0 || pending.ClassListContains("shiori-hidden"))
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("history / pending changes did not load");
                yield return null;
            }
            Assert.That(view.Q<Label>("view-error").ClassListContains("shiori-hidden"), Is.True, view.Q<Label>("view-error").text);

            // The pending changes (a.txt modified, new.txt untracked) are listed under the 保存 button.
            Assert.That(pending.itemsSource.Count, Is.EqualTo(2));
            Assert.That(view.Q<Label>("save-status").text, Does.Contain("2"));
        }

        [UnityTest]
        public IEnumerator SimpleModeView_ListsSetAsideChanges()
        {
            yield return PrepareRepository();
            var head = _session.Repository.GetHeadAsync(CancellationToken.None);
            yield return Await(head);
            // 「保存せずに戻す」 to the current snapshot: the unsaved edits are set aside.
            var restore = RestoreRunner.RunAsync(_session.Repository, head.Result, "最初", RestoreMode.StashFirst, null, CancellationToken.None);
            yield return Await(restore);
            Assume.That(restore.Result.StashHash, Is.Not.Null);

            var view = new SimpleModeView(_session);
            var panel = view.Q<Foldout>("aside-panel");
            Assert.That(panel.ClassListContains("shiori-hidden"), Is.True, "hidden until loaded");
            view.RefreshAll();
            var started = System.DateTime.UtcNow;
            while (panel.ClassListContains("shiori-hidden"))
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("the set-aside changes were not listed");
                yield return null;
            }

            Assert.That(panel.text, Is.EqualTo(L10n.Tr("aside.title", 1)));
            var rows = view.Q<VisualElement>("aside-list");
            Assert.That(rows.childCount, Is.EqualTo(1));
            Assert.That(rows[0].Q<Label>().text, Is.EqualTo(L10n.Tr("aside.item", "最初")));
            Assert.That(rows[0].Q<Button>().text, Is.EqualTo(L10n.Tr("aside.button")));
        }

        [UnityTest]
        public IEnumerator SimpleModeView_SendRowFollowsTheRemote()
        {
            yield return PrepareRepository();
            var view = new SimpleModeView(_session);
            var row = view.Q<VisualElement>("send-row");
            view.RefreshAll();
            var started = System.DateTime.UtcNow;
            while (row.ClassListContains("shiori-hidden"))
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("the send row was not shown");
                yield return null;
            }
            Assert.That(view.Q<Button>("send-button").text, Is.EqualTo(L10n.Tr("send.setup")));
            Assert.That(view.Q<Label>("send-status").text, Is.EqualTo(L10n.Tr("send.none")));

            yield return Await(_session.Repository.SetRemoteUrlAsync("https://example.invalid/shiori.git", CancellationToken.None));
            view.RefreshAll();
            started = System.DateTime.UtcNow;
            while (view.Q<Button>("send-button").text != L10n.Tr("send.button"))
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("the send button did not switch");
                yield return null;
            }
            Assert.That(view.Q<Label>("send-status").text, Is.EqualTo(L10n.Tr("send.never", 1)), "counted locally, no network needed");
            Assert.That(view.Q<Button>("receive-button").ClassListContains("shiori-hidden"), Is.False, "受信 appears with a 送信先");
            Assert.That(view.Q<Button>("receive-button").text, Is.EqualTo(L10n.Tr("receive.button")));
        }

        [Test]
        public void ReceiveMessages_AndTheCombinedStatusLine()
        {
            var sent = new SendStatus("u", 0, false);
            Assert.That(RemoteText.Status(sent, new RemoteComparison(true, 0, 2)), Is.EqualTo(L10n.Tr("receive.waiting", 2)));
            Assert.That(RemoteText.Status(new SendStatus("u", 1, false), new RemoteComparison(true, 0, 3)),
                Is.EqualTo(L10n.Tr("send.unsent", 1) + " ・ " + L10n.Tr("receive.waiting", 3)));
            Assert.That(RemoteText.Status(sent, new RemoteComparison(true, 1, 1)), Is.EqualTo(L10n.Tr("receive.split")));
            Assert.That(RemoteText.Status(sent, RemoteComparison.Unknown), Is.EqualTo(L10n.Tr("send.uptodate")));
            Assert.That(RemoteText.Describe(new ReceiveResult(ReceiveOutcome.Received, 4)), Is.EqualTo(L10n.Tr("receive.done", 4)));
            Assert.That(RemoteText.Describe(new ReceiveResult(ReceiveOutcome.Diverged, 2)), Is.EqualTo(L10n.Tr("receive.diverged", 2)));
            foreach (ReceiveOutcome outcome in System.Enum.GetValues(typeof(ReceiveOutcome)))
            {
                Assert.That(RemoteText.Describe(new ReceiveResult(outcome)), Is.Not.Empty.And.Not.StartWith("receive."), outcome.ToString());
            }
            Assert.That(RemoteWatch.Interval, Is.GreaterThanOrEqualTo(System.TimeSpan.FromMinutes(1)), "never a tight polling loop");
        }

        [Test]
        public void SimpleModeView_PanelsScrollTogether()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), System.Array.Empty<ShioriExtension>());
            var task = session.LocateGitAsync(CancellationToken.None);
            // LocateGitAsync may complete synchronously from the cache; the view only needs the repository object.
            Assume.That(task.IsCompleted && session.Repository != null, Is.True, "git must already be located in this domain");
            var view = new SimpleModeView(session);
            var scroll = view.Q<ScrollView>("simple-scroll");
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.mode, Is.EqualTo(ScrollViewMode.Vertical));
            foreach (var name in new[] { "save-panel", "aside-panel", "history-panel", "detail-panel", "ext-notices" })
            {
                Assert.That(scroll.contentContainer.Q(name), Is.Not.Null, name + " is inside the scroll area");
            }
            Assert.That(scroll.contentContainer.ClassListContains("shiori-simple-body"), Is.True);
        }

        [Test]
        public void SendMessages_ExplainEachOutcome()
        {
            Assert.That(RemoteText.Describe(new SendResult(SendOutcome.Sent, 3, false)), Is.EqualTo(L10n.Tr("send.done", 3)));
            Assert.That(RemoteText.Describe(new SendResult(SendOutcome.Sent, 1, true)), Is.EqualTo(L10n.Tr("send.done", 1) + "\n" + L10n.Tr("send.unsaved")));
            Assert.That(RemoteText.Describe(new SendResult(SendOutcome.NothingToSend, 0, false)), Is.EqualTo(L10n.Tr("send.nothing")));
            Assert.That(RemoteText.Status(new SendStatus("u", 0, false)), Is.EqualTo(L10n.Tr("send.uptodate")));
            Assert.That(RemoteText.Status(new SendStatus("u", 2, false)), Is.EqualTo(L10n.Tr("send.unsent", 2)));
            Assert.That(RemoteText.Status(null), Is.EqualTo(L10n.Tr("send.none")));
            var tooLarge = new RemoteOperationException(RemoteErrorKind.TooLarge, "push", 1, "remote: error: GH001: Large files detected.");
            Assert.That(RemoteText.Describe(tooLarge), Does.StartWith(L10n.Tr("remote.error.TooLarge")).And.Contains("GH001"));
            var auth = new RemoteOperationException(RemoteErrorKind.Authentication, "push", 128, "fatal: Authentication failed");
            Assert.That(RemoteText.Describe(auth), Is.EqualTo(L10n.Tr("remote.error.Authentication")), "no raw git text for known causes");
            foreach (RemoteErrorKind kind in System.Enum.GetValues(typeof(RemoteErrorKind)))
            {
                Assert.That(L10n.Tr("remote.error." + kind), Is.Not.EqualTo("remote.error." + kind), "a Japanese text exists for " + kind);
            }
        }

        [Test]
        public void TakeOutMessages_ExplainEachOutcome()
        {
            var paths = new[] { "a", "b", "c", "d", "e", "f", "g" };
            Assert.That(SimpleModeView.DescribeTakeOut(new SetAsidePlan(SetAsideBlock.None, paths, null)), Is.EqualTo(L10n.Tr("aside.done", 7)));
            Assert.That(SimpleModeView.DescribeTakeOut(new SetAsidePlan(SetAsideBlock.WorkingTreeHasChanges, paths, null)), Is.EqualTo(L10n.Tr("aside.dirty")));
            Assert.That(SimpleModeView.ListPaths(paths, 5), Is.EqualTo("a\nb\nc\nd\ne\n" + L10n.Tr("aside.more", 2)));
            Assert.That(SimpleModeView.DescribeTakeOut(new SetAsidePlan(SetAsideBlock.Overlap, paths, new[] { "x" })), Is.EqualTo(L10n.Tr("aside.overlap", "x")));
        }

        [UnityTest]
        public IEnumerator SetupWizardView_Builds()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), System.Array.Empty<ShioriExtension>());
            var status = session.EvaluateSetupAsync(CancellationToken.None);
            yield return Await(status);
            var view = new SetupWizardView(session, status.Result);
            var steps = view.Q<VisualElement>("steps");
            Assert.That(steps.childCount, Is.EqualTo(5), "four steps plus the optional 送信先");
            Assert.That(steps[4].Q<Label>("step-title").text, Is.EqualTo(L10n.Tr("step.remote.title")));
            Assert.That(steps[4].Q<Label>("step-state").text, Is.EqualTo(L10n.Tr("step.remote.optional")));
            Assert.That(status.Result.StepCount, Is.EqualTo(4), "the optional step is not counted");
        }

        [UnityTest]
        public IEnumerator SetupWizardView_ShowsExtensionStepsBetweenIgnoreFilesAndFirstSave()
        {
            var extension = new FakeExtension();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { extension });
            var status = session.EvaluateSetupAsync(CancellationToken.None);
            yield return Await(status);
            Assert.That(status.Result.StepCount, Is.EqualTo(5));
            Assert.That(status.Result.FirstSaveStep, Is.EqualTo(5));

            var view = new SetupWizardView(session, status.Result);
            var steps = view.Q<VisualElement>("steps");
            Assert.That(steps.childCount, Is.EqualTo(6));
            Assert.That(steps[3].Q<Label>("step-title").text, Is.EqualTo(FakeExtension.StepTitle));
            Assert.That(steps[3].Q<Label>("step-message").text, Is.EqualTo(FakeExtension.TodoMessage));
            Assert.That(steps[4].Q<Label>("step-title").text, Is.EqualTo(L10n.Tr("step4.title")));
        }

        [UnityTest]
        public IEnumerator SimpleModeView_ShowsMemoPlaceholderAndPendingExtensionStepAsNotice()
        {
            yield return PrepareRepository();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { new FakeExtension() });
            yield return Await(session.LocateGitAsync(CancellationToken.None));

            var view = new SimpleModeView(session);
            var placeholder = view.Q<Label>("save-placeholder");
            Assert.That(placeholder.text, Is.EqualTo(FakeExtension.MemoPlaceholder));
            Assert.That(placeholder.ClassListContains("shiori-hidden"), Is.False, "empty memo shows the placeholder");
            view.DraftMessage = "typed";
            Assert.That(placeholder.ClassListContains("shiori-hidden"), Is.True, "a memo hides the placeholder");

            var memo = view.Q<TextField>("save-message");
            var counter = view.Q<Label>("memo-counter");
            Assert.That(memo.maxLength, Is.EqualTo(SimpleModeView.MemoMaxLength));
            Assert.That(counter.ClassListContains("shiori-hidden"), Is.True, "short memos show no counter");
            view.DraftMessage = new string('x', SimpleModeView.MemoCounterFrom);
            Assert.That(counter.text, Is.EqualTo(SimpleModeView.MemoCounterFrom + "/" + SimpleModeView.MemoMaxLength));
            Assert.That(counter.ClassListContains("shiori-hidden"), Is.False);
            Assert.That(counter.ClassListContains("shiori-memo-counter--full"), Is.False);
            view.DraftMessage = new string('x', 100);
            Assert.That(view.DraftMessage.Length, Is.EqualTo(SimpleModeView.MemoMaxLength), "a restored draft is cut to the cap");
            Assert.That(counter.ClassListContains("shiori-memo-counter--full"), Is.True);
            Assert.That(view.Q<Button>("save-button").tooltip, Is.EqualTo(L10n.Tr("simple.save.tooltip")));
            view.DraftMessage = string.Empty;

            // The fake step is pending (its block is not in .gitignore), so it shows up as a notice with its button.
            view.RefreshAll();
            var notices = view.Q<VisualElement>("ext-notices");
            var started = System.DateTime.UtcNow;
            while (notices.childCount == 0)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("the pending extension step was not shown");
                yield return null;
            }
            Assert.That(notices.ClassListContains("shiori-hidden"), Is.False);
            Assert.That(notices[0].Q<Label>().text, Is.EqualTo(FakeExtension.StepTitle));
            Assert.That(notices[0].Q<Button>().text, Is.EqualTo("Write"));

            // An extension notice joins the pending step once its cause exists.
            File.WriteAllText(Path.Combine(_root, FakeExtension.NoticeFile), "x");
            view.RefreshStatus();
            started = System.DateTime.UtcNow;
            while (notices.childCount < 2)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("the extension notice was not shown");
                yield return null;
            }
            Assert.That(notices[1].Q<Label>().text, Is.EqualTo(FakeExtension.NoticeTitle));
            Assert.That(notices[1].Q<Button>().text, Is.EqualTo("Fix"));

            var noExtensions = new ShioriSession(_root, new ProcessGitRunner(), System.Array.Empty<ShioriExtension>());
            yield return Await(noExtensions.LocateGitAsync(CancellationToken.None));
            var plain = new SimpleModeView(noExtensions);
            Assert.That(plain.Q<Label>("save-placeholder").ClassListContains("shiori-hidden"), Is.True, "no extension, no placeholder");
            Assert.That(plain.Q<VisualElement>("ext-notices").ClassListContains("shiori-hidden"), Is.True);
        }
    }
}
