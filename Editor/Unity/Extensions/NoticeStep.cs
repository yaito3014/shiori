using System.Threading;
using System.Threading.Tasks;

namespace Shiori.Editor
{
    /// <summary>
    /// Presents an <see cref="ExtensionNotice"/> through the same panel as a pending setup step.
    /// The view is fixed; after a button runs the simple view asks every extension for its notices again.
    /// </summary>
    internal sealed class NoticeStep : SetupStep
    {
        public ExtensionNotice Notice { get; }

        public NoticeStep(ExtensionNotice notice)
        {
            Notice = notice;
        }

        public override string Id => Notice.Id;

        public override string Title => Notice.Title;

        public SetupStepView View => new SetupStepView(false, Notice.Message, Notice.Detail, Notice.Actions);

        public override Task<SetupStepView> EvaluateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(View);
        }

        public static ExtensionStepStatus ToStatus(ShioriExtension extension, ExtensionNotice notice)
        {
            var step = new NoticeStep(notice);
            return new ExtensionStepStatus(extension, step) { View = step.View };
        }
    }
}
