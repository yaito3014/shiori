# 0009 アニメーターコントローラーの Unity 表示は、型ごとの規則で読む

## 状況

詳細モードの Unity 表示（ADR 0005）は .prefab と .mat だけだった。VRChat の利用者が主に編集するのは
.controller と .anim で、YAML のテキスト差分では読めない。.controller をそのまま今の差分に通すと、
遷移は m_Name が空なので名前がなく、パラメーターやステートの一覧は位置で比べるので途中に 1 つ足すと後ろが全部変わって見え、
Animator ウィンドウでノードを動かしただけでも m_Position の変更が出る。

## 選択

- `UnityYamlDiff` に型ごとの規則（`TypeRules`）を足し、AnimatorController / AnimatorStateMachine / AnimatorState /
  AnimatorStateTransition / AnimatorTransition にだけ当てる。プレハブとマテリアルの比べ方は変えない（テストで固定）。
- 一覧の対応付け: パラメーターは名前、レイヤーはステートマシンの参照（同期レイヤーなど無いときは名前）、
  子ステートと子ステートマシンは参照先、遷移とビヘイビアの一覧は参照そのもの。レイヤー・遷移・ビヘイビアは順番に意味がある
  （上書きの優先度、遷移の優先度）ので、順番が変わったら「[A, B] → [B, A]」を 1 行出す。
- グラフ上の位置（m_Position、m_AnyStatePosition など）は出さない。
- 名前: ステートは「ステートマシン/ステート」、遷移は「元 → 先」（Any State、Entry、Exit、子ステートマシンから出る遷移を含む）、
  ステートのビヘイビアは持ち主のステートの場所。
- m_Type と m_ConditionMode は Unity の enum 名（Float / Int / Bool / Trigger、If / IfNot / Greater / Less / Equals / NotEqual）で出す。
  値は 6000.6 のエディターのアセンブリから読んだ。
- BlendTree の子は今までどおり位置で比べる。.anim は別の段階（カーブの対応付けが要る）。

## 理由

- 型で分ければ、`m_Position` のような一般的な名前を隠しても、プレハブのコンポーネントの同名フィールドに影響しない。
- 参照（fileID）は Unity が保存のたびに保つので、名前の変更にも強い。名前しかないパラメーターは名前で合わせ、
  名前の変更は削除と追加に見えることを受け入れる。

## 捨てた案

- すべてのファイルで「m_Name を持つ map の一覧は名前で合わせる」を一律に当てる: プレハブの表示が変わる。
- ノードの位置も出す: 動かしただけの保存が、意味のある変更と同じに見える。
- enum を数字のまま出す: 4 が Bool、6 が Equals だと覚えている人は少ない。
