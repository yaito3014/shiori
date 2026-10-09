# 更新履歴 / Changelog

リリースごとに、使う人向けの言葉で書いています。開発者向けの詳細は各リリースの GitHub Releases にあります。
Each release is described for the people who use it. The developer-level detail is in the GitHub release notes.

## [Unreleased]

### 日本語

- **取ってある変更を取り出せるようになりました。** 「保存せずに戻す」で取っておいた変更が、かんたんモードの
  「取ってある変更」に並び、「取り出す」で今のプロジェクトに戻せます。取っておいたあとの履歴と同じファイルを
  変えているときは、混ざらないように取り出さず、そのファイルを知らせます。
- かんたんモードの言葉に「送信」「受信」を加えました（今後のリモート機能で使います）。

### English

- **Set-aside changes can be brought back.** Changes kept by 「保存せずに戻す」 are listed under
  「取ってある変更」 in simple mode, and 取り出す applies them to the project. When a file also changed in
  the history since, nothing is applied and the file is named, so changes never get mixed.
- The simple-mode vocabulary gains 送信 / 受信 for the upcoming remote features.

## [0.1.1] - 2026-10-09

### 日本語

- 配布されるパッケージを `Editor/`、`package.json`、`LICENSE` だけにしました。
  テストや開発者向けの文書は入らなくなり、利用者のプロジェクトの Test Runner に Shiori のテストが出ることもなくなります。
  動作に変わりはありません。

### English

- The distributed package now contains only `Editor/`, `package.json` and `LICENSE`. Tests and
  developer documents are no longer shipped, so Shiori's tests no longer appear in a project's
  Test Runner. No change in behaviour.

## [0.1.0] - 2026-10-09

### 日本語

- **はじめの設定**: git の確認、プロジェクト設定、履歴に含めないもの、最初の保存の 4 手順を上から順に進めます。
  `.gitignore` / `.gitattributes` には Shiori のブロックを追記するだけで、手で書いた行は残します。
- **かんたんモード**: メモを書いて「保存」（または Enter）で今の状態を履歴に残します。メモは 60 文字まで。
  「履歴」で保存した時点が新しい順に並び、選ぶと変わったファイルが見えます。
  「この時点に戻す」でプロジェクト全体をその時点に戻せます。未保存の変更は「保存してから戻す」か、
  捨てずに別に取っておきます。
- **詳細モード**（閲覧のみ）: 変更ファイルの一覧と、選んだファイルの差分。
- **安全のために**: コンパイル中・再生中・処理中はボタンが止まり、上部に理由が出ます。
  `.meta` ファイルの抜けや余りを保存前に知らせます。
- **設定**: `Preferences > Shiori`（git の場所、使うモード）と `Project Settings > Shiori`（はじめの設定の状態）。
- **拡張パッケージ**: VRChat 向けなど、環境ごとの機能を別パッケージとして足せる仕組み
  （はじめの設定の追加手順、右上の状態表示、メモ欄の例文、戻す前の注意）。

### English

- **Setup wizard**: four steps, top to bottom: find git, project settings, what to keep out of the
  history, first save. Shiori appends its own blocks to `.gitignore` / `.gitattributes` and leaves
  hand-written lines alone.
- **Simple mode**: write a memo and press 保存 (or Enter) to record the project as it is now; memos
  are up to 60 characters. 履歴 lists saved points newest first and shows which files changed.
  この時点に戻す returns the whole project to that point; unsaved changes are either saved first or
  set aside, never thrown away.
- **Detail mode** (read-only): list of changed files and the diff of the selected one.
- **Safety**: buttons are disabled while compiling, in play mode or while a task runs, with the reason
  shown at the top. Missing or orphaned `.meta` files are reported before saving.
- **Settings**: `Preferences > Shiori` (git path, modes) and `Project Settings > Shiori` (setup state).
- **Extension packages**: environment-specific additions such as VRChat ship as separate packages
  that add wizard steps, a status chip, a memo placeholder and restore warnings.
