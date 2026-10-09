using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// Entry point of an add-on package (for example <c>com.yaito3014.shiori.vrchat</c>).
    /// <c>Shiori.Editor</c> finds every concrete subclass with <c>TypeCache</c> and creates it with
    /// its public parameterless constructor; the core works unchanged when there are none.
    /// Every member has a do-nothing default so a newer core can add members without breaking
    /// extensions built against an older one. Extensions never show UI themselves: they return
    /// text and actions, and the core renders them with its own widgets and vocabulary.
    /// </summary>
    public abstract class ShioriExtension
    {
        /// <summary>The add-on's package name. Keys its section in <c>ProjectSettings/Shiori.json</c> and its managed blocks.</summary>
        public abstract string PackageId { get; }

        /// <summary>
        /// Extra setup-wizard steps, shown after the core's 履歴に含めないもの step and before 最初の保存.
        /// Also listed under Project Settings so the user can run them again later.
        /// </summary>
        public virtual IReadOnlyList<SetupStep> CreateSetupSteps(IExtensionContext context)
        {
            return Array.Empty<SetupStep>();
        }

        /// <summary>
        /// Localized placeholder shown inside the empty メモ field (for example memo examples for
        /// the kind of project the extension targets), or null for the core's default (none).
        /// </summary>
        public virtual string GetMemoPlaceholder(IExtensionContext context)
        {
            return null;
        }

        /// <summary>A short localized chip shown in the window header (for example the build target), or null.</summary>
        public virtual string GetStatusLine(IExtensionContext context)
        {
            return null;
        }

        /// <summary>A localized warning appended to the 戻す confirmation, or null when there is nothing to warn about.</summary>
        public virtual string GetRestoreWarning(IExtensionContext context, Snapshot target)
        {
            return null;
        }

        /// <summary>
        /// Like <see cref="GetRestoreWarning"/>, but with the paths the 戻す will change, and async so the
        /// extension can read files as they are in the target (<see cref="IGitRepository.ReadFileAtAsync"/>).
        /// The core calls this one; the default falls back to <see cref="GetRestoreWarning"/>.
        /// </summary>
        public virtual Task<string> GetRestoreWarningAsync(IExtensionContext context, RestorePreview preview, CancellationToken cancellationToken)
        {
            return Task.FromResult(GetRestoreWarning(context, preview?.Target));
        }

        /// <summary>
        /// Notices to show at the top of simple mode right now. Called whenever the view refreshes, so
        /// return them from the project's current state rather than remembering events.
        /// </summary>
        public virtual Task<IReadOnlyList<ExtensionNotice>> GetNoticesAsync(IExtensionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ExtensionNotice>>(Array.Empty<ExtensionNotice>());
        }

        /// <summary>Runs after Unity flushed its edits to disk and before the working tree is recorded.</summary>
        public virtual Task BeforeSaveAsync(IExtensionContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>Runs after 保存 recorded a snapshot. <paramref name="commitHash"/> is null when there was nothing to save.</summary>
        public virtual Task AfterSaveAsync(IExtensionContext context, string commitHash, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>Runs after 戻す finished and the asset database was refreshed.</summary>
        public virtual Task AfterRestoreAsync(IExtensionContext context, RestoreResult result, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
