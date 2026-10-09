using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// One setup-wizard step contributed by a <see cref="ShioriExtension"/>. The step owns the
    /// logic (what is missing, how to fix it) and describes itself as a <see cref="SetupStepView"/>;
    /// the core draws it. <see cref="EvaluateAsync"/> is called again after every action.
    /// </summary>
    public abstract class SetupStep
    {
        /// <summary>Stable identifier, used in error messages and tests (for example <c>vrchat.ignore</c>).</summary>
        public abstract string Id { get; }

        /// <summary>Localized title shown in the step header.</summary>
        public abstract string Title { get; }

        /// <summary>Checks the project and describes the current state. Must not change anything.</summary>
        public abstract Task<SetupStepView> EvaluateAsync(CancellationToken cancellationToken);
    }

    /// <summary>What a step looks like right now: done or not, an explanation, and the buttons to offer.</summary>
    public sealed class SetupStepView
    {
        public bool Done { get; }

        /// <summary>Localized main text: what is missing, or that everything is fine.</summary>
        public string Message { get; }

        /// <summary>Localized secondary text (current values, file lists), or empty.</summary>
        public string Detail { get; }

        /// <summary>Buttons shown while the step is enabled. Usually empty when <see cref="Done"/> is true.</summary>
        public IReadOnlyList<SetupStepAction> Actions { get; }

        public SetupStepView(bool done, string message, string detail = null, IReadOnlyList<SetupStepAction> actions = null)
        {
            Done = done;
            Message = message ?? string.Empty;
            Detail = detail ?? string.Empty;
            Actions = actions ?? Array.Empty<SetupStepAction>();
        }
    }

    /// <summary>A button on a step. <see cref="Run"/> is awaited on the editor's main thread and may throw; the error is shown in the step.</summary>
    public sealed class SetupStepAction
    {
        public string Label { get; }
        public Func<CancellationToken, Task> Run { get; }

        public SetupStepAction(string label, Func<CancellationToken, Task> run)
        {
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("label is required", nameof(label));
            Label = label;
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Convenience for synchronous actions.</summary>
        public SetupStepAction(string label, Action run)
            : this(label, _ =>
            {
                if (run == null) throw new ArgumentNullException(nameof(run));
                run();
                return Task.CompletedTask;
            })
        {
        }
    }
}
