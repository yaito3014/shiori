using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori.Editor.Tests
{
    /// <summary>
    /// A minimal extension with one step that is done once its block is in .gitignore.
    /// Lives in a *.Tests assembly, so <see cref="ExtensionRegistry"/> never picks it up in a real project.
    /// </summary>
    internal sealed class FakeExtension : ShioriExtension
    {
        public const string Id = "com.example.fake";
        public const string BlockId = "fake-ext";
        public const string StepTitle = "Fake step";
        public const string TodoMessage = "fake block missing";
        public const string DoneMessage = "fake block present";
        public const string StatusLine = "fake status";
        public const string MemoPlaceholder = "fake placeholder";
        public const string RestoreWarning = "fake warning";

        public static readonly string[] Lines = { "FakeFolder/", "*.fake" };

        public int BeforeSaveCalls;
        public int AfterSaveCalls;
        public int AfterRestoreCalls;
        public string LastCommitHash;

        public override string PackageId => Id;

        public override IReadOnlyList<SetupStep> CreateSetupSteps(IExtensionContext context)
        {
            return new SetupStep[] { new BlockStep(context) };
        }

        public override string GetMemoPlaceholder(IExtensionContext context) => MemoPlaceholder;

        public override string GetStatusLine(IExtensionContext context) => StatusLine;

        public override string GetRestoreWarning(IExtensionContext context, Snapshot target) => RestoreWarning;

        public const string NoticeFile = "Assets/notice.txt";
        public const string NoticeId = "fake.notice";
        public const string NoticeTitle = "Fake notice";

        /// <summary>The async warning names how many paths the 戻す changes, to prove the preview arrives.</summary>
        public override Task<string> GetRestoreWarningAsync(IExtensionContext context, RestorePreview preview, CancellationToken cancellationToken)
        {
            return Task.FromResult(RestoreWarning + " (" + preview.ChangedPaths.Count + ")");
        }

        /// <summary>A notice derived from state: shown while <see cref="NoticeFile"/> exists.</summary>
        public override Task<IReadOnlyList<ExtensionNotice>> GetNoticesAsync(IExtensionContext context, CancellationToken cancellationToken)
        {
            var path = System.IO.Path.Combine(context.ProjectRoot, NoticeFile);
            IReadOnlyList<ExtensionNotice> notices = System.IO.File.Exists(path)
                ? new[] { new ExtensionNotice(NoticeId, NoticeTitle, "notice message", "notice detail", new[] { new SetupStepAction("Fix", () => System.IO.File.Delete(path)) }) }
                : System.Array.Empty<ExtensionNotice>();
            return Task.FromResult(notices);
        }

        public override Task BeforeSaveAsync(IExtensionContext context, CancellationToken cancellationToken)
        {
            BeforeSaveCalls++;
            return Task.CompletedTask;
        }

        public override Task AfterSaveAsync(IExtensionContext context, string commitHash, CancellationToken cancellationToken)
        {
            AfterSaveCalls++;
            LastCommitHash = commitHash;
            return Task.CompletedTask;
        }

        public override Task AfterRestoreAsync(IExtensionContext context, RestoreResult result, CancellationToken cancellationToken)
        {
            AfterRestoreCalls++;
            return Task.CompletedTask;
        }

        private sealed class BlockStep : SetupStep
        {
            private readonly IExtensionContext _context;

            public BlockStep(IExtensionContext context)
            {
                _context = context;
            }

            public override string Id => "fake.block";
            public override string Title => StepTitle;

            public override Task<SetupStepView> EvaluateAsync(CancellationToken cancellationToken)
            {
                var current = _context.ReadManagedBlock(".gitignore", BlockId);
                var done = current != null && current.Count == Lines.Length;
                var view = done
                    ? new SetupStepView(true, DoneMessage)
                    : new SetupStepView(false, TodoMessage, null, new[] { new SetupStepAction("Write", () => _context.UpsertManagedBlock(".gitignore", BlockId, Lines)) });
                return Task.FromResult(view);
            }
        }
    }
}
