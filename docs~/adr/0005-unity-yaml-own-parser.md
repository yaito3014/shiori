# 0005 Unity YAML は Core 内の小さなパーサで読む

## 状況

詳細モードで .prefab / .mat の変更を「GameObject・コンポーネント・プロパティ」単位で見せたい（提案 A3）。
CLAUDE.md では「自前パーサか、Unity 同梱の YamlDotNet に依存するか」が未決定だった。ユーザーは自前パーサを選んだ。

## 選択

- `Shiori.Core` に `UnityYamlParser`（約 400 行）と `UnityYamlDiff` を置く。Unity にも外部 DLL にも依存しない。
- 読むのは Unity が書く範囲だけ: `--- !u!<class> &<fileID> [stripped]`、インデントのブロック map / sequence
  （キーと同じ深さに書かれる sequence を含む）、flow map / flow sequence、プレーン・単引用符・二重引用符の
  スカラー（複数行の折り返しを含む）、`|` / `>` のブロックスカラー。読めない行は `(raw)` として残し、例外にしない。
- 差分はオブジェクトを fileID で対応付け、プロパティを `m_LocalPosition.x` のような道筋で並べる。
  マテリアルのプロパティ一覧（1 キーの map の列）はキー名で対応付ける。`serializedVersion` など Unity が
  意味なく書き換える欄は出さない。参照は同じファイル内なら「Transform (Avatar/Hat)」、アセットなら
  `IGuidResolver`（Unity 側は AssetDatabase）でパスにする。GameObject の場所は Transform の m_Father をたどる。
- 最初の版は .prefab と .mat の作業ツリー差分だけ、読み取りのみ。テキスト差分にいつでも切り替えられる。

## 理由

- VRChat のプロジェクトには YamlDotNet の別版が入っていることがあり、DLL の衝突を避けたい（ADR 0001 と同じ考え）。
- Unity の YAML は狭い部分集合で、汎用パーサの機能（アンカー、タグ解決、型変換）はほとんど要らない。
- Core に置けば NUnit だけでテストできる。

## 捨てた案

- YamlDotNet に依存: 上記の衝突と、Unity 独自のヘッダ（`!u!` タグ、stripped）を結局自前で扱う必要がある。
- テキスト差分の行から推測する: どのオブジェクトの行かがわからない。
- シーン（.unity）も最初から対象にする: 大きく、PrefabInstance の上書き（m_Modifications）の表示を別に考える必要がある。
