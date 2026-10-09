using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>F7: Preferences > Shiori (per user) and Project Settings > Shiori (per project), both UI Toolkit.</summary>
    internal static class ShioriSettingsProviders
    {
        public const string PreferencesPath = "Preferences/Shiori";
        public const string ProjectPath = "Project/Shiori";

        [SettingsProvider]
        public static SettingsProvider CreatePreferencesProvider()
        {
            return new SettingsProvider(PreferencesPath, SettingsScope.User)
            {
                label = L10n.Tr("settings.title"),
                keywords = new HashSet<string>(new[] { "Shiori", "git", "履歴" }),
                activateHandler = (searchContext, root) => BuildPreferences(root),
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectProvider()
        {
            return new SettingsProvider(ProjectPath, SettingsScope.Project)
            {
                label = L10n.Tr("settings.title"),
                keywords = new HashSet<string>(new[] { "Shiori", "git", "履歴", "gitignore" }),
                activateHandler = (searchContext, root) => BuildProject(root),
            };
        }

        // ---- Preferences > Shiori ----

        internal static void BuildPreferences(VisualElement root)
        {
            var session = TryCreateSession(root);
            if (session == null) return;
            var page = NewPage(root, L10n.Tr("settings.title"));

            // git path
            page.Add(SectionTitle(L10n.Tr("settings.git.title")));
            page.Add(Help(L10n.Tr("settings.git.help")));
            var pathField = new TextField(L10n.Tr("settings.git.path")) { value = session.User.GitPath ?? string.Empty };
            pathField.AddToClassList("shiori-settings-field");
            var result = Help(string.Empty);
            var buttons = new VisualElement();
            buttons.AddToClassList("shiori-step-controls-row");
            var browse = new Button { text = L10n.Tr("settings.git.browse") };
            var auto = new Button { text = L10n.Tr("settings.git.auto") };
            buttons.Add(browse);
            buttons.Add(auto);
            page.Add(pathField);
            page.Add(buttons);
            page.Add(result);

            void ApplyGitPath(string value)
            {
                session.User.GitPath = value?.Trim() ?? string.Empty;
                pathField.SetValueWithoutNotify(session.User.GitPath);
                Save(() => session.SaveUserSettings(), result);
                ShowGitLocation(session, result);
                ShioriSettingsEvents.RaiseChanged();
            }

            pathField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (pathField.value.Trim() != (session.User.GitPath ?? string.Empty)) ApplyGitPath(pathField.value);
            });
            pathField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == UnityEngine.KeyCode.Return || e.keyCode == UnityEngine.KeyCode.KeypadEnter) ApplyGitPath(pathField.value);
            });
            browse.clicked += () =>
            {
                var picked = EditorUtility.OpenFilePanel(L10n.Tr("settings.git.browse.title"), string.Empty, "exe");
                if (!string.IsNullOrEmpty(picked)) ApplyGitPath(picked);
            };
            auto.clicked += () => ApplyGitPath(string.Empty);
            ShowGitLocation(session, result);

            // modes
            page.Add(SectionTitle(L10n.Tr("settings.mode.title")));
            var availabilityChoices = new List<string> { L10n.Tr("settings.modes.both"), L10n.Tr("settings.modes.simple"), L10n.Tr("settings.modes.detail") };
            var availabilityField = new DropdownField(L10n.Tr("settings.modes.label"), availabilityChoices, (int)session.User.Modes);
            availabilityField.AddToClassList("shiori-settings-field");
            page.Add(availabilityField);
            page.Add(Help(L10n.Tr("settings.modes.help")));

            availabilityField.RegisterValueChangedCallback(e =>
            {
                session.User.Modes = (ModeAvailability)Math.Max(0, availabilityField.index);
                Save(() => session.SaveUserSettings(), result);
                ShioriSettingsEvents.RaiseChanged();
            });

            page.Add(Help(L10n.Tr("settings.user.file")));
        }

        private static async void ShowGitLocation(ShioriSession session, Label result)
        {
            result.text = L10n.Tr("status.checking");
            try
            {
                var git = await session.LocateGitAsync(CancellationToken.None, refresh: true);
                if (!git.Found) result.text = L10n.Tr("settings.git.notfound");
                else if (!git.MeetsMinimumVersion) result.text = L10n.Tr("step1.tooold", git.Version);
                else result.text = L10n.Tr("step1.found", git.Version) + "\n" + L10n.Tr("step1.path", git.Path);
            }
            catch (Exception ex)
            {
                result.text = L10n.Tr("error.generic", ex.Message);
            }
        }

        // ---- Project Settings > Shiori ----

        internal static void BuildProject(VisualElement root)
        {
            var session = TryCreateSession(root);
            if (session == null) return;
            var page = NewPage(root, L10n.Tr("settings.title"));
            var status = Help(string.Empty);

            page.Add(SectionTitle(L10n.Tr("settings.setup.title")));
            var completed = new Toggle(L10n.Tr("settings.setup.completed")) { value = session.Project.SetupCompleted };
            completed.AddToClassList("shiori-settings-field");
            completed.RegisterValueChangedCallback(e =>
            {
                session.Project.SetupCompleted = e.newValue;
                Save(() => session.SaveProjectSettings(), status);
                ShioriSettingsEvents.RaiseChanged();
            });
            page.Add(completed);
            page.Add(Help(L10n.Tr("settings.setup.help")));

            page.Add(SectionTitle(L10n.Tr("settings.ignore.title")));
            var ignoreStatus = Help(string.Empty);
            var rewrite = new Button { text = L10n.Tr("step3.apply") };
            rewrite.AddToClassList("shiori-small-button");
            void RenderIgnore()
            {
                ignoreStatus.text = session.AreIgnoreFilesWritten() ? L10n.Tr("step3.ok") : L10n.Tr("settings.ignore.missing");
            }
            rewrite.clicked += () =>
            {
                try
                {
                    session.WriteIgnoreFiles();
                }
                catch (Exception ex)
                {
                    status.text = L10n.Tr("error.generic", ex.Message);
                }
                RenderIgnore();
                ShioriSettingsEvents.RaiseChanged();
            };
            RenderIgnore();
            page.Add(ignoreStatus);
            page.Add(rewrite);

            // Extension steps (for example VRChat's ignore list) can be run again from here after setup.
            var extensions = new VisualElement();
            page.Add(extensions);
            FillExtensionSteps(extensions, session, status);

            page.Add(Help(L10n.Tr("settings.project.file")));
            page.Add(status);
        }

        private static async void FillExtensionSteps(VisualElement root, ShioriSession session, Label status)
        {
            if (session.Extensions.Count == 0) return;
            try
            {
                var steps = await session.EvaluateExtensionStepsAsync(CancellationToken.None);
                if (root.panel == null) return;
                if (steps.Count == 0) return;
                root.Add(SectionTitle(L10n.Tr("settings.ext.title")));
                foreach (var step in steps)
                {
                    var panel = new ExtensionStepPanel(step);
                    panel.Changed += ShioriSettingsEvents.RaiseChanged;
                    root.Add(panel);
                }
            }
            catch (Exception ex)
            {
                status.text = L10n.Tr("error.generic", ex.Message);
            }
        }

        // ---- helpers ----

        private static ShioriSession TryCreateSession(VisualElement root)
        {
            try
            {
                return new ShioriSession(ShioriSession.DetectProjectRoot());
            }
            catch (SettingsFormatException ex)
            {
                var page = NewPage(root, L10n.Tr("settings.title"));
                var error = new Label(L10n.Tr("error.settings", ex.Message));
                error.AddToClassList("shiori-error");
                page.Add(error);
                return null;
            }
        }

        private static VisualElement NewPage(VisualElement root, string title)
        {
            try
            {
                root.styleSheets.Add(UiAssets.Style("ShioriWindow.uss"));
            }
            catch (InvalidOperationException)
            {
                // Styling is optional for the settings pages.
            }
            // The Settings window draws the provider label itself; no heading of our own.
            var page = new VisualElement();
            page.AddToClassList("shiori-settings");
            root.Add(page);
            return page;
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("shiori-settings-section");
            return label;
        }

        private static Label Help(string text)
        {
            var label = new Label(text);
            label.AddToClassList("shiori-settings-help");
            return label;
        }

        private static void Save(Action save, Label status)
        {
            try
            {
                save();
                status.text = string.Empty;
            }
            catch (Exception ex)
            {
                status.text = L10n.Tr("error.generic", ex.Message);
            }
        }
    }
}
