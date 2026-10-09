# 0002 拡張 API は Shiori.Core の抽象クラスと DTO にし、UI を持たせない

## 状況

`shiori-vrchat` がウィザードの追加ステップ、`.gitignore` の独自ブロック、保存 / 戻す の前後フック、
かんたんモードへの文言追加を必要とする。コアは拡張がゼロでも従来どおり動かなければならない。

## 選択

- 契約は `Shiori.Core` の `ShioriExtension`（抽象クラス、全メンバーに no-op 既定値）、
  `SetupStep` / `SetupStepView` / `SetupStepAction`、`IExtensionContext` に置く。
- ステップは「完了か / 文言 / ボタン」を DTO で返すだけで、`VisualElement` を返さない。
  描画は `Shiori.Editor` が既存の `SetupStep.uxml` で行う。
- 発見は `Shiori.Editor` の `ExtensionRegistry` が `TypeCache.GetTypesDerivedFrom<ShioriExtension>()` で行い、
  `*.Tests` アセンブリの型は除外する。
- `ManagedBlockWriter` にブロック ID を持たせ（既定 `shiori`、拡張はパッケージ名）、
  1 ファイルに複数ブロックを共存させる。拡張は `IExtensionContext` 経由でしか書けず、
  対象は `.gitignore` / `.gitattributes` の 2 つに限る。
- `ProjectSettings/Shiori.json` に `extensions: { "<packageId>": {...} }` を追加し、未インストールの
  パッケージの節もそのまま残す。空の節は書かない。
- `MiniJson` を public にし、拡張が小さな JSON（VPM マニフェストなど）を読めるようにする。

## 理由

- 「public API は Core のインターフェースと DTO のみ」の規約を崩さずに済む。
- 拡張を NUnit だけ（Unity API なし）でテストできる。
- 新メンバーを virtual で足せるので、古い拡張を壊さずにコアを進められる。

## 捨てた案

- `Shiori.Editor` に public な抽象クラスを置き、ステップが `VisualElement` を返す: 自由度は高いが、
  拡張側のテストに Unity が必要になり、UI 語彙（保存 / 履歴 / 戻す）の統制もコアから離れる。
- 拡張用ローカライズテーブルをコアに登録させる: 文言は拡張が自分の表で解決して渡せば足りる。
  コアは `IExtensionContext.LanguageCode` を渡すだけにした。
- `InternalsVisibleTo("Shiori.VRChat.Editor")`: 特定パッケージ名への結合を避けた。
