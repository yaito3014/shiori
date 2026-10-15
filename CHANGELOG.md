# 更新履歴 / Changelog

リリースごとに、使う人向けの言葉で書いています。開発者向けの詳細は各リリースの GitHub Releases にあります。
Each release is described for the people who use it. The developer-level detail is in the GitHub release notes.

## [Unreleased]

### 日本語

- **受信で、ほかの PC の保存と合わせられるようになりました。** これまでは、2 台の PC で別々に保存すると受信が止まっていました。
  今は両方を合わせた保存を作ります。シーンやプレハブは Unity の道具（UnityYAMLMerge）がオブジェクトごとに合わせます。
  合わせられなかったファイルは、ファイルごとに「自分のを使う」「相手のを使う」を選べます。「やめる」で受信する前に戻せます。
- **はじめの設定に「Unity のマージ設定」を足しました。** すでに使っているプロジェクトでは、かんたんモードの上に出る
  「設定する」を押してください。Unity を入れ替えたときは「更新する」が出ます。「使わない」も選べ、あとから
  `Project Settings > Shiori` の「使うのをやめる」でやめることもできます（確認があります）。
- 保存していない変更があるときの受信は、確認のあと先に保存してから受信します。
- 詳細モードのプルとマージも、競合したら止めて、ファイルごとに ours / theirs を選べるようになりました。
- かんたんモードの上に出るお知らせ（まだの設定や、パッケージがそろっていないなど）を、詳細モードにも 1 行で出すようにしました。
  説明はマウスを乗せると出ます。
- **大きなファイル（Git LFS）に対応しました。** はじめの設定に「大きなファイルの設定」を足しました（任意）。
  画像・モデル・音・動画を Git LFS で保存します。すでに使っているプロジェクトでは、上に出る「設定する」か「使わない」を
  押してください。これから保存するファイルから LFS に入り、それまでの履歴は変えません。
- LFS を使うプロジェクトなのにこの PC で LFS が使えないときは、受信・戻す・バリエーションの切り替えを止め、
  そろえ方を案内します（画像などの代わりに中身のないファイルが入るのを防ぐため）。
- 大きなファイルの設定は、あとから `Project Settings > Shiori` の「使うのをやめる」でやめられます（確認があります）。

### English

- **受信 merges with the other PC's saves**: when two PCs saved separately, 受信 used to stop; now it makes a save that
  combines both. Scenes and prefabs are merged object by object by Unity's UnityYAMLMerge. Files it cannot combine are
  decided one by one (自分 / 相手), and やめる goes back to before the 受信.
- **New setup step 「Unity のマージ設定」**: projects already set up get a 設定する button at the top of simple mode, and
  更新する after a Unity upgrade. It is optional: 使わない skips it, and 「使うのをやめる」 in Project Settings > Shiori
  turns it off again after a confirmation.
- 受信 with unsaved changes now saves them first, after asking.
- Detail mode's pull and merge stop on conflicts and let you pick ours / theirs per file.
- The notices at the top of simple mode (pending setup, packages out of sync, ...) now also appear in detail mode, one
  line each with the heading and buttons; hover for the explanation.
- **Git LFS support**: a new, optional setup step 「大きなファイルの設定」 stores images, models, audio and video through
  Git LFS. Projects already set up get 設定する / 使わない at the top. Only new saves go to LFS; history is not rewritten.
- When a project uses Git LFS but this PC cannot run it, 受信, 戻す and switching variations wait until it is set up,
  so placeholder pointer files never replace textures and models.
- Git LFS can be turned off again from Project Settings > Shiori (「使うのをやめる」, after a confirmation). Files now in
  LFS are staged again as ordinary files and go into the next save.

## [0.5.0] - 2026-10-14

### 日本語

- **バリエーションが使えるようになりました。** 同じプロジェクトの別の版（衣装違いなど）を作って行き来できます。
  保存と履歴はバリエーションごとに分かれます。切り替える前に、変わるファイルの数と、Unity が読み込み直すことを知らせます。
  保存していない変更は、保存してから切り替えるか、そのまま持っていくかを選べます。
  ほかの PC で作ったバリエーションも、受信のあと一覧に出ます。削除は確認のあと、送信先からも消します。
- **詳細モードに「ブランチ」タブを足しました。** 作成・切り替え・マージ・削除ができます。マージは作業ツリーがきれいなときだけで、
  競合したら中止して何も変えません。

### English

- **Variations**: make other versions of the same project (say, a different outfit) and switch between them. Each
  variation has its own saves and history. Switching says how many files change and that Unity will re-import them;
  unsaved changes are either saved first or carried along. Variations made on another PC appear after 受信. Deleting
  asks first and also removes the variation from the 送信先.
- **Branch tab in detail mode**: create, switch, merge and delete branches. Merging needs a clean working tree and
  is aborted, changing nothing, when it conflicts.

## [0.4.0] - 2026-10-13

### 日本語

- **詳細モードで、プレハブとマテリアルの変更を Unity の言葉で見られるようになりました。**
  どの GameObject のどのコンポーネントの、どのプロパティが、どう変わったか（例: `m_LocalPosition.y: 1.5 → 1.75`）を並べます。
  テクスチャなどの参照はアセットのパスで表示します。「テキスト」に切り替えると、今までの差分表示に戻ります。
- **詳細モードで、ファイルごとにステージ / ステージから外せるようになりました。**
  「変更」タブが「ステージ済み」と「未ステージ」に分かれ、ステージしたものだけをコミットできます。
  アセットと .meta は 1 行にまとまり、一緒にステージされるので、片方だけコミットしてしまうことはありません。
  何もステージしていなければ、これまでどおりすべての変更をコミットします。

### English

- **Detail mode shows prefab and material changes in Unity terms**: which GameObject, component and property changed and
  how (for example `m_LocalPosition.y: 1.5 → 1.75`), with referenced assets shown by path. 「テキスト」 switches back to
  the text diff.
- **Detail mode can stage and unstage files**: the changes tab is split into staged and unstaged lists, and only what
  is staged is committed. An asset and its .meta share one row and are staged together, so one is never committed without the other.
  With nothing staged, commit still takes every change as before.

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
