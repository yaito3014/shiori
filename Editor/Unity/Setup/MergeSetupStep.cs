using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// The core's own steps, contributed through the extension API so they appear in the wizard, as a notice in simple
    /// mode when pending (say, after a Unity upgrade moved the tool), and under Project Settings, like any add-on's.
    /// </summary>
    internal sealed class CoreSetupSteps : ShioriExtension
    {
        public const string Id = "com.yaito3014.shiori";

        public override string PackageId => Id;

        public override IReadOnlyList<SetupStep> CreateSetupSteps(IExtensionContext context)
        {
            return new SetupStep[] { new MergeSetupStep(() => context.Repository, context), new LargeFileSetupStep(context) };
        }
    }

    /// <summary>
    /// 「Unity のマージ設定」: UnityYAMLMerge as git's merge driver (<see cref="MergeDriverSetup"/>), pointing at the
    /// running editor's copy, so 受信 and merges combine scenes and prefabs object by object. Recommended, not required:
    /// 「使わない」 / 「使うのをやめる」 record the choice in ProjectSettings/Shiori.json when a context is given.
    /// </summary>
    internal sealed class MergeSetupStep : SetupStep
    {
        /// <summary>Key in the core's section of ProjectSettings/Shiori.json; <see cref="Off"/> when the project goes without.</summary>
        public const string SettingKey = "mergeDriver";
        public const string Off = "off";

        private readonly Func<IGitRepository> _repository;
        private readonly IExtensionContext _context;
        private readonly string _toolPath;

        public MergeSetupStep(Func<IGitRepository> repository, IExtensionContext context = null) : this(repository, ToolPath, context)
        {
        }

        internal MergeSetupStep(Func<IGitRepository> repository, string toolPath, IExtensionContext context = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _toolPath = toolPath;
            _context = context;
        }

        private bool Skipped => _context != null && _context.Settings.TryGetValue(SettingKey, out var value) && value as string == Off;

        /// <summary>UnityYAMLMerge next to the running editor (Editor/Data/Tools on Windows, Contents/Tools on macOS).</summary>
        public static string ToolPath
        {
            get
            {
                var name = Path.DirectorySeparatorChar == '\\' ? "UnityYAMLMerge.exe" : "UnityYAMLMerge";
                return Path.Combine(EditorApplication.applicationContentsPath, "Tools", name);
            }
        }

        public override string Id => "shiori.merge";

        public override string Title => L10n.Tr("step.merge.title");

        public override async Task<SetupStepView> EvaluateAsync(CancellationToken cancellationToken)
        {
            var repository = _repository();
            // Without git the earlier step is not done; this one waits silently.
            if (repository == null) return new SetupStepView(false, L10n.Tr("step.merge.explain"));

            var state = await MergeDriverSetup.EvaluateAsync(repository, _toolPath, cancellationToken, Skipped);
            switch (state)
            {
                case MergeDriverState.Ready:
                    return _context == null
                        ? new SetupStepView(true, L10n.Tr("step.merge.ok"))
                        : new SetupStepView(true, L10n.Tr("step.merge.ok"), null, new[] { new SetupStepAction(L10n.Tr("step.merge.disable"), DisableAsync) });
                case MergeDriverState.Skipped:
                    return new SetupStepView(true, L10n.Tr("step.merge.skipped"), null, new[] { Apply("step.merge.use") });
                case MergeDriverState.ToolMissing:
                    // Nothing can be set up; merges still work, only without Unity's help.
                    return new SetupStepView(true, L10n.Tr("step.merge.notool"), _toolPath);
                case MergeDriverState.RootMismatch:
                    return new SetupStepView(false, L10n.Tr("step.merge.mismatch"));
                case MergeDriverState.Outdated:
                    return new SetupStepView(false, L10n.Tr("step.merge.outdated"), _toolPath, new[] { Apply("step.merge.update") });
                default:
                    return new SetupStepView(false, L10n.Tr("step.merge.explain"), _toolPath,
                        _context == null
                            ? new[] { Apply("step.merge.apply") }
                            : new[] { Apply("step.merge.apply"), new SetupStepAction(L10n.Tr("step.merge.skip"), () => Record(true)) });
            }
        }

        private SetupStepAction Apply(string labelKey)
        {
            return new SetupStepAction(L10n.Tr(labelKey), async ct =>
            {
                await MergeDriverSetup.ApplyAsync(_repository(), _toolPath, ct);
                Record(false);
            });
        }

        private void Record(bool off)
        {
            if (_context == null) return;
            if (off) _context.Settings[SettingKey] = Off;
            else if (!_context.Settings.Remove(SettingKey)) return;
            _context.SaveSettings();
        }

        /// <summary>Asks first; scenes and prefabs then go back to git's own line merge.</summary>
        private async Task DisableAsync(CancellationToken cancellationToken)
        {
            if (!EditorUtility.DisplayDialog(L10n.Tr("merge.disable.title"), L10n.Tr("merge.disable.body"), L10n.Tr("merge.disable.ok"), L10n.Tr("restore.dialog.cancel"))) return;
            await MergeDriverSetup.DisableAsync(_repository(), cancellationToken);
            Record(true);
        }
    }
}
