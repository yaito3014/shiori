# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Extension API for add-on packages (`ShioriExtension` in `Shiori.Core`): wizard steps rendered by
  the core from UI-free `SetupStepView`s, save / restore hooks, status line and save hint in simple
  mode, restore-dialog warnings, a per-package section in `ProjectSettings/Shiori.json`, and
  managed blocks with ids so several packages can share `.gitignore` / `.gitattributes`.
  Extensions are discovered with `TypeCache`; the core works unchanged without any.
- Project Settings > Shiori lists extension steps so they can be run again after setup.
- Setup wizard: git detection with guidance, Force Text / Visible Meta Files,
  managed `.gitignore` / `.gitattributes` blocks, first snapshot with identity entry.
- Simple mode (かんたん): 保存 with memo and generated message, pending-change list,
  `.meta` consistency warnings, 履歴 with relative time and per-snapshot file list,
  戻す with save-first / stash-first confirmation and a linear `Restore:` commit.
- Detail mode (詳細), read-only: working-tree change list and coloured unified diff.
- Editor-state lock (compiling, updating, play mode, git running) with a reason banner.
- Settings pages under Preferences and Project Settings (git path, mode, setup flag).
- `Shiori.Core`: git runner, locator, repository wrapper with porcelain parsers,
  ManagedBlockWriter, MetaChecker, SettingsStore, RestoreRunner, DiffParser
  (all Unity-independent).
- Package skeleton, dev-project scripts (`Tools~/`) and CI on 2022.3 / Unity 6.
