# 0001 設定ファイルの JSON 実装は Core 内の最小実装にする

## 状況

`ProjectSettings/Shiori.json` と `UserSettings/Shiori.json` を読み書きする必要がある。
`Shiori.Core` は UnityEngine を参照しないので `JsonUtility` は使えず、
.NET Standard 2.1 には `System.Text.Json` がない。

## 選択

`Editor/Core/Settings/MiniJson.cs` に 300 行程度の JSON リーダ / ライタを置き、
`SettingsStore` がそれを使って辞書 ⇔ DTO を手で詰め替える。
未知のキーは無視し、欠けたキーは既定値にする。

## 理由

- 扱う JSON はフラットなオブジェクト 2 つだけで、汎用シリアライザは過剰。
- 外部依存を増やすと `shiori-vrchat` 側や利用者プロジェクトとの DLL 衝突の火種になる。
- 出力形式（キー順、インデント、LF）を固定でき、設定ファイルの diff が安定する。

## 捨てた案

- `com.unity.nuget.newtonsoft-json` への依存: Core を Unity パッケージ依存にしたくない。
  利用者プロジェクトに別バージョンがあると衝突しうる。
- `JsonUtility` を `Shiori.Editor` 側で使い、Core は文字列だけ扱う: 設定の読み書きが
  Core の責務（CLAUDE.md）なので層分けが崩れる。
