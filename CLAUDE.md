# Shiori for Unity — Claude Code 向け作業ルール

Unity Editor 内で動く Git クライアント。非プログラマー向けの「かんたんモード」と
通常の Git クライアント相当の「詳細モード」を同じデータモデルの上に載せる。
本リポジトリは Unity 汎用のコアパッケージ `com.yaito3014.shiori`。
VRChat 固有の機能（VPM プリセット、かんたん UI の VRChat 向け調整）は
将来 `shiori-vrchat` という別リポジトリ・別パッケージに置く。本リポジトリには
VRChat SDK への依存を一切入れない。

マイルストーン仕様はリポジトリに置かない（ユーザーが会話に貼り付けて渡す）。
仕様に書かれていないことを実装する前に、まずユーザーに確認すること。

## 環境制約（必ず守る）

- 対応 Unity: **2022.3 以上**。`package.json` の `"unity"` は `"2022.3"`。
- Unity 6（6000.x）でもコンパイル・テストが通ることを維持する。
- 言語機能は **C# 9 / .NET Standard 2.1** の範囲に限定する。
  `record struct`、`required`、file-scoped namespace、raw string literal、
  list pattern などの C# 10 以降の構文は使わない。
- Unity 6 で obsolete になった API は使わない（例: `FindObjectsOfType` →
  `FindObjectsByType`）。両バージョンに存在する API を優先し、
  どうしても分岐が必要な場合のみ `#if UNITY_6000_0_OR_NEWER` を使う。
- `UnityEditor.VersionControl` の Provider 系 API には依存しない。
- 各 asmdef と同じフォルダの `csc.rsp` に `-warnaserror+:CS0618` を置く
  （Unity は asmdef のフォルダ直下の csc.rsp しか読まないため、`Editor/` 直下には置かない）。
  obsolete API の使用はコンパイルエラーとして扱う。
- 主対象 OS は Windows。macOS はパス処理で壊さない程度に配慮する
  （`Path.Combine`、`/` と `\` の混在禁止）。

## リポジトリ構成

```
shiori-unity/
  package.json              # com.yaito3014.shiori（ルートがパッケージ本体）
  README.md
  CHANGELOG.md
  LICENSE                   # MIT
  Editor/
    Core/                   # Shiori.Core.asmdef + csc.rsp — Unity 非依存（noEngineReferences: true）
    Unity/                  # Shiori.Editor.asmdef + csc.rsp — Unity 連携・UI
      UI/                   # UXML / USS / EditorWindow
      Localization/         # 文字列テーブル（ja / en）
  Tests/
    Editor/
      Core/                 # Shiori.Core.Tests.asmdef（NUnit、Unity API 不使用）
      Unity/                # Shiori.Editor.Tests.asmdef
  Samples~/
  Tools~/                   # 開発用スクリプト（配布対象外）
    New-DevProject.ps1      # 検証用 Unity プロジェクトをリポジトリの外に生成する
    Test-DevProject.ps1     # 検証用プロジェクトで EditMode テストを回し、結果を要約する
    Export-UnityPackage.ps1 # 追跡ファイルから ../dist/<name>-<version>.unitypackage を作る（Unity 不要）
    Install-Hooks.ps1       # 上を post-commit フックとして入れる（shiori-vrchat にも使う）
  docs~/
    adr/                    # 設計判断の記録（後述）。末尾 ~ なので Unity からも配布物からも外れる
  .github/workflows/

../shiori-dev/              # 検証用 Unity プロジェクト（リポジトリの外。インストール済みバージョンごとに 1 つ）
  2022.3/                   # 2022.3.22f1
  6000.6/                   # 6000.6.0f1
    Packages/manifest.json  # "com.yaito3014.shiori": "file:../../../shiori"
```

検証用プロジェクトをリポジトリの中に置かない理由: ウィザードがリポジトリの入れ子
（`rev-parse --show-toplevel` がプロジェクトルートと異なる）を検出して止まるため。

- `Shiori.Core` は **UnityEngine / UnityEditor を参照しない**。git CLI の呼び出し、
  リポジトリ状態のモデル、設定ファイルの読み書き、Unity YAML の解析（将来）は
  すべてここに置く。Unity 固有のものを入れたくなったら設計を見直す。
- `Shiori.Editor` が `Shiori.Core` を参照する。逆方向の参照は禁止。
- 名前空間は `Shiori` と `Shiori.Editor`。`Shiori.VRChat` は本リポジトリに作らない。
- 末尾 `~` のフォルダは Unity に無視される。配布に含めたくないものはそこに置く。

## ビルドと検証

Claude Code は Editor の GUI を開けない。できるのはコンパイルと EditMode テストの
実行まで。GUI の見た目・操作感はユーザーが確認するので、UI を変更したときは
「どこを見て何を試してほしいか」を短く書いて渡すこと。

```
# 検証用プロジェクトの生成（既定の出力先: ../shiori-dev/<major.minor>）
pwsh Tools~/New-DevProject.ps1 -UnityVersion 2022.3.22f1

# EditMode テスト（結果の要約と失敗したテストのメッセージを表示する）
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 6000.6.0f1

# コンパイルのみ
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1 -CompileOnly
```

- インストール済み Unity: 2022.3.22f1 / 6000.6.0f1（`C:/Program Files/Unity/Hub/Editor/<version>/Editor/Unity.exe`）。
- Unity を直接呼ぶ場合、`-testResults` の相対パスは projectPath 基準で解決される
  （カレントディレクトリ基準ではない）。
- `Shiori.Core.Tests` は Unity 非依存なので、将来 `dotnet test` だけで回せる
  構成に移してもよい（M1 では Unity Test Runner で統一する）。
- CI（GitHub Actions + GameCI）はローカルと同じバージョン（2022.3.22f1 / 6000.6.0f1）で
  EditMode テストを回す。プロジェクトは `Tools~/New-DevProject.ps1` で `ci-project~/` に生成する
  （末尾 `~` がないと、リポジトリ＝パッケージなので Unity がプロジェクトごと取り込んで壊れる）。
  Secrets `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` が必要。
- 変更をコミットする前に、インストール済みの全バージョンで `Test-DevProject.ps1` を通す。
- リリース: `package.json` の version を上げて `vX.Y.Z` タグを push すると `.github/workflows/release.yml` が
  `git archive` の zip（`Tools~` / `.github` / dotfiles は `.gitattributes` の `export-ignore` で除外）と
  `package.json`、SHA-256 を GitHub Release に添付し、変数 `VPM_LISTING_REPO` と秘密 `VPM_LISTING_TOKEN` が
  あれば `../vpm-listing` に `repository_dispatch` を送る。VCC / ALCOM にはそのリスティングから届く。
- コミットごとに `.unitypackage` をワークスペース直下の `dist/` に出す。`git` の post-commit フックが
  `Tools~/Export-UnityPackage.ps1` を呼ぶ（`.git/hooks` は追跡されないので、クローン直後は
  `pwsh Tools~/Install-Hooks.ps1` で入れる）。中身は `git ls-files` の `.meta` 付きファイルだけで、
  pathname は `Packages/<name>/...`。Unity を起動しないので数秒で終わる。

## Git バックエンドの方針

- **git CLI を `System.Diagnostics.Process` で呼ぶ。** LibGit2Sharp やその他の
  ネイティブライブラリは使わない。
- 必要最低バージョンは git 2.30。起動時に `git --version` を確認し、
  見つからない・古い場合はウィザードで案内する（自動インストールはしない）。
- git の探索順: ユーザー設定で指定されたパス → `PATH` → Git for Windows の既定パス
  （`C:\Program Files\Git\cmd\git.exe`）。
- 認証は git 側（credential manager）に全面委譲する。トークンやパスワードを
  本パッケージで保持・保存・表示しない。
- すべての git 呼び出しは非同期（`Task`）で行い、Editor のメインスレッドを塞がない。
  結果の UI 反映は `SynchronizationContext` 経由でメインスレッドに戻す。
  長時間かかる操作は `UnityEditor.Progress` で進捗を出す。
- 出力のパースには `--porcelain` 系（`status --porcelain=v2 -z`、
  `log --format=...` の独自区切り）を使い、人間向け出力をパースしない。
- git の作業ディレクトリは Unity プロジェクトのルート（`Assets/` の親）。
  サブディレクトリに `.git` がある構成は M1 では非対応（検出してエラー表示）。

## 安全規則（例外なし）

以下は **ユーザーが UI 上で明示的に確認した場合にのみ** 実行する。
確認ダイアログなしに呼ぶコードを書かない。

- `git reset --hard`、`git checkout -- <path>`、`git restore`（作業ツリーを捨てる系）
- `git clean`（`-n` のドライランを除く）
- `git push --force` / `--force-with-lease`
- `git branch -D`、`git stash drop`、`git reflog expire`、`git gc --prune`

さらに:

- コンパイル中（`EditorApplication.isCompiling`）、Play モード中、
  ドメインリロード中は git 操作を一切発行しない。UI 側も操作を無効化する。
- 既存の `.gitignore` / `.gitattributes` は上書きしない。必要な行を
  マーカー付きブロックとして追記し、再実行時はそのブロックだけ更新する。
- `Library/` を削除・再生成させる操作を暗黙に行わない。
- 作業ツリーのファイルを書き換える操作（戻す、ブランチ切り替え）の前後で
  `AssetDatabase.DisallowAutoRefresh` / `AllowAutoRefresh` と `Refresh` を対にして呼ぶ。
- ユーザーのプロジェクト内のファイルを、本パッケージの設定ファイル以外
  （`ProjectSettings/Shiori.json`、`UserSettings/Shiori.json`、
  `.gitignore` / `.gitattributes` の自ブロック）に勝手に書き込まない。

## コーディング規約

- UI は **UI Toolkit**（UXML + USS + C#）。IMGUI は使わない。
- UI の表示文字列はコードに直書きせず `Editor/Unity/Localization/` の
  テーブルから引く。M1 では ja を埋め、en はキーをそのまま出してよい。
- かんたんモードの語彙は固定: **保存 / 履歴 / 戻す / バリエーション / 同期**。
  かんたんモードの UI に commit / stage / branch / push / pull という語を出さない。
- `public` API は `Shiori.Core` のインターフェースと DTO のみ。実装クラスは `internal`。
  テストから触る場合は `InternalsVisibleTo` を使う。
- 例外はログに握り潰さない。git の失敗は `GitException`（終了コード・stderr 付き）
  として伝播させ、UI 層でユーザー向け文言に変換する。
- 1 コミット 1 目的。コミットメッセージは英語の Conventional Commits
  （`feat:` `fix:` `test:` `docs:` `chore:`）。

## 設計判断の記録

仕様にない設計上の選択をしたときは `docs~/adr/NNNN-<slug>.md` に
「状況 / 選択 / 理由 / 捨てた案」を 10 行程度で残す。

## まだ決まっていないこと（勝手に決めない）

次の点は未決定。実装が必要になったら着手前にユーザーに聞くこと。

- Unity YAML（シーン / プレハブ）の意味的差分の実装方式
  （自前パーサか、Unity 同梱の YamlDotNet に依存するか）
- 競合解決の UI（UnityYAMLMerge 失敗時のフォールバック表示）
- サムネイルの保存先（`refs/notes/shiori` か orphan ブランチか）
- VPM リポジトリ（index.json）の公開手順と、`shiori-vrchat` への分割タイミング
- 英語ローカライズを正式対応にする時期
- LFS を「必須」にするか「推奨」に留めるか（M1 では検出と案内のみ）
