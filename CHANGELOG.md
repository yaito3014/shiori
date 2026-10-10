# 更新履歴 / Changelog

リリースごとに、使う人向けの言葉で書いています。開発者向けの詳細は各リリースの GitHub Releases にあります。
Each release is described for the people who use it. The developer-level detail is in the GitHub release notes.

## [0.3.0] - 2026-10-10

### 日本語

- **受信できるようになりました。** ほかの PC から送信された保存を受け取り、プロジェクトを最新にします。
  保存していない変更があるときや、この PC とほかの PC で別々に保存していたときは、何も変えずに理由を知らせます。
- かんたんモードが数分ごとに送信先を確かめ、受信できる保存があると知らせます。この確認でサインイン画面は出ません。
- **詳細モードで操作できるようになりました。** コミット、プッシュ、プル（fast-forward のみ）、ログからの「この状態に戻す」、
  スタッシュの適用ができます。中身はかんたんモードと同じ処理です。

### English

- **受信 (receive).** Saves sent from another PC are received and the project moves forward to them (fast-forward
  only). With unsaved changes, or when this PC and another one saved separately, nothing changes and the reason is shown.
- Simple mode checks the 送信先 every few minutes and shows how many saves are waiting, without ever opening a sign-in window.
- **Detail mode can act, not just show.** Commit, push, pull (fast-forward only), restore to a commit from the log,
  and apply any stash, using the same flows and safety checks as simple mode. Tabs: changes, log, stashes.

## [0.2.0] - 2026-10-10

### 日本語

- **取ってある変更を取り出せるようになりました。** 「保存せずに戻す」で取っておいた変更が、かんたんモードの
  「取ってある変更」に並び、「取り出す」で今のプロジェクトに戻せます。取っておいたあとの履歴と同じファイルを
  変えているときは、混ざらないように取り出さず、そのファイルを知らせます。
- **送信できるようになりました。** 保存した履歴を、GitHub などに作った送信先に送ります。かんたんモードに
  「送信」ボタンと、まだ送っていない保存の数が出ます。送信先は、はじめの設定の最後（任意）か Project Settings で設定します。
  サインインや接続の失敗、大きすぎるファイル（GitHub では 1 ファイル 100 MB まで）は、わかりやすい言葉で知らせます。
- かんたんモードの言葉に「送信」「受信」を加えました。受信は次の版で入ります。
- ウィンドウが小さいときは、かんたんモード全体をスクロールできるようにしました（今までは下が切れていました）。
- 処理中に設定を変えたとき、表示が更新されないことがあったのを直しました。
- 拡張パッケージが失敗したとき、どのパッケージかを Console に出すようにしました。

### English

- **Set-aside changes can be brought back.** Changes kept by 「保存せずに戻す」 are listed under
  「取ってある変更」 in simple mode, and 取り出す applies them to the project. When a file also changed in
  the history since, nothing is applied and the file is named, so changes never get mixed.
- **送信 (send).** Saved history can be pushed to a 送信先 such as an empty private GitHub repository. Simple mode
  shows a 送信 button and how many saves are not sent yet. The 送信先 is set in an optional last setup step or in
  Project Settings. Sign-in, connection, size-limit (GitHub: 100 MB per file) and rejection failures get plain messages.
- The simple-mode vocabulary gains 送信 / 受信. 受信 (receive) comes in the next version.
- Simple mode scrolls as a whole in a short window instead of cutting off the lower panels.
- A refresh requested while the view was busy is no longer dropped (a new 送信先 could stay invisible).
- A failing extension package is named in the Console.

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
