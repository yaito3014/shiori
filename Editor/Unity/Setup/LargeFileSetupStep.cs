using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Shiori.Editor
{
    /// <summary>
    /// 「大きなファイルの設定」: images, models, audio and video through Git LFS (<see cref="LargeFileSetup"/>).
    /// Recommended, not required: 「使わない」 records the choice in ProjectSettings/Shiori.json, so every PC sees it.
    /// </summary>
    internal sealed class LargeFileSetupStep : SetupStep
    {
        /// <summary>Key in the core's section of ProjectSettings/Shiori.json; <see cref="Off"/> when the project goes without.</summary>
        public const string SettingKey = "largeFiles";
        public const string Off = "off";

        private readonly IExtensionContext _context;

        public LargeFileSetupStep(IExtensionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public override string Id => "shiori.lfs";

        public override string Title => L10n.Tr("step.lfs.title");

        private bool Skipped => _context.Settings.TryGetValue(SettingKey, out var value) && value as string == Off;

        public override async Task<SetupStepView> EvaluateAsync(CancellationToken cancellationToken)
        {
            var repository = _context.Repository;
            // Without git the earlier step is not done; this one waits silently.
            if (repository == null) return new SetupStepView(false, L10n.Tr("step.lfs.explain"));

            var status = await LargeFileSetup.EvaluateAsync(repository, Skipped, cancellationToken);
            switch (status.State)
            {
                case LargeFileState.Ready:
                    // Only Shiori's own block can be taken out again; the user's own LFS lines are theirs.
                    return status.HasBlock
                        ? new SetupStepView(true, L10n.Tr("step.lfs.ok"), null, new[] { new SetupStepAction(L10n.Tr("step.lfs.disable"), DisableAsync) })
                        : new SetupStepView(true, L10n.Tr("step.lfs.ok.own"));
                case LargeFileState.Skipped:
                    return new SetupStepView(true, L10n.Tr("step.lfs.skipped"), null, new[] { Apply("step.lfs.use") });
                case LargeFileState.Outdated:
                    return new SetupStepView(false, L10n.Tr("step.lfs.outdated"), Summary(), new[] { Apply("step.lfs.update") });
                case LargeFileState.NeedsInstall:
                    return new SetupStepView(false, L10n.Tr("step.lfs.install"), null, new[] { new SetupStepAction(L10n.Tr("step.lfs.install.button"), InstallHere) });
                case LargeFileState.ToolMissing:
                    return status.UsedByProject
                        ? new SetupStepView(false, L10n.Tr("step.lfs.missing.used"), LargeFileSetup.DownloadUrl, new[] { Download() })
                        : new SetupStepView(false, L10n.Tr("step.lfs.missing"), LargeFileSetup.DownloadUrl, new[] { Download(), Skip() });
                case LargeFileState.RootMismatch:
                    return new SetupStepView(false, L10n.Tr("step.lfs.mismatch"));
                default:
                    return new SetupStepView(false, L10n.Tr("step.lfs.explain"), Summary(), new[] { Apply("step.lfs.apply"), Skip() });
            }
        }

        private static string Summary()
        {
            var extensions = new List<string>();
            foreach (var extension in LargeFileSetup.Extensions) extensions.Add("." + extension);
            return L10n.Tr("step.lfs.detail", string.Join(" ", extensions));
        }

        private SetupStepAction Apply(string labelKey)
        {
            return new SetupStepAction(L10n.Tr(labelKey), async ct =>
            {
                await LargeFileSetup.ApplyAsync(_context.Repository, ct);
                if (_context.Settings.Remove(SettingKey)) _context.SaveSettings();
            });
        }

        private SetupStepAction Skip()
        {
            return new SetupStepAction(L10n.Tr("step.lfs.skip"), () =>
            {
                _context.Settings[SettingKey] = Off;
                _context.SaveSettings();
            });
        }

        private static SetupStepAction Download()
        {
            // The step is evaluated again after the button; once git-lfs is installed, the next refresh moves on.
            return new SetupStepAction(L10n.Tr("step.lfs.download"), () => Application.OpenURL(LargeFileSetup.DownloadUrl));
        }

        /// <summary>GitHub refuses ordinary files over 100 MB, which LFS used to carry.</summary>
        internal const long SendLimitBytes = 100L * 1024 * 1024;

        /// <summary>Asks first: the next save grows by every file now in LFS, and old saves still need LFS.</summary>
        private async Task DisableAsync(CancellationToken cancellationToken)
        {
            var repository = _context.Repository;
            var files = await repository.ListLfsFilesAsync(cancellationToken);
            var tooLarge = 0;
            foreach (var path in files)
            {
                var info = new System.IO.FileInfo(System.IO.Path.Combine(repository.WorkingDirectory, path));
                if (info.Exists && info.Length > SendLimitBytes) tooLarge++;
            }

            var body = L10n.Tr("lfs.disable.body", files.Count);
            if (tooLarge > 0) body += "\n\n" + L10n.Tr("lfs.disable.toolarge", tooLarge);
            if (!EditorUtility.DisplayDialog(L10n.Tr("lfs.disable.title"), body, L10n.Tr("lfs.disable.ok"), L10n.Tr("restore.dialog.cancel"))) return;

            await LargeFileSetup.DisableAsync(repository, files, cancellationToken);
            _context.Settings[SettingKey] = Off;
            _context.SaveSettings();
        }

        /// <summary>Downloads the real files over the pointers, so the AssetDatabase waits and then imports them once.</summary>
        private async Task InstallHere(CancellationToken cancellationToken)
        {
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                using (GitActivity.Begin(L10n.Tr("step.lfs.install.progress")))
                {
                    await LargeFileSetup.InstallAndPullAsync(_context.Repository, cancellationToken);
                }
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
            }
        }
    }
}
