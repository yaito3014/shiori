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
            return new SetupStep[] { new MergeSetupStep(() => context.Repository) };
        }
    }

    /// <summary>
    /// 「Unity のマージ設定」: UnityYAMLMerge as git's merge driver (<see cref="MergeDriverSetup"/>), pointing at the
    /// running editor's copy, so 受信 and merges combine scenes and prefabs object by object.
    /// </summary>
    internal sealed class MergeSetupStep : SetupStep
    {
        private readonly Func<IGitRepository> _repository;
        private readonly string _toolPath;

        public MergeSetupStep(Func<IGitRepository> repository) : this(repository, ToolPath)
        {
        }

        internal MergeSetupStep(Func<IGitRepository> repository, string toolPath)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _toolPath = toolPath;
        }

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

            var state = await MergeDriverSetup.EvaluateAsync(repository, _toolPath, cancellationToken);
            switch (state)
            {
                case MergeDriverState.Ready:
                    return new SetupStepView(true, L10n.Tr("step.merge.ok"));
                case MergeDriverState.ToolMissing:
                    // Nothing can be set up; merges still work, only without Unity's help.
                    return new SetupStepView(true, L10n.Tr("step.merge.notool"), _toolPath);
                case MergeDriverState.RootMismatch:
                    return new SetupStepView(false, L10n.Tr("step.merge.mismatch"));
                case MergeDriverState.Outdated:
                    return new SetupStepView(false, L10n.Tr("step.merge.outdated"), _toolPath, new[] { Apply("step.merge.update") });
                default:
                    return new SetupStepView(false, L10n.Tr("step.merge.explain"), _toolPath, new[] { Apply("step.merge.apply") });
            }
        }

        private SetupStepAction Apply(string labelKey)
        {
            return new SetupStepAction(L10n.Tr(labelKey), ct => MergeDriverSetup.ApplyAsync(_repository(), _toolPath, ct));
        }
    }
}
