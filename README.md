# Shiori for Unity

Unity Editor の中で動く Git クライアントです。Git を知らない人向けの「かんたんモード」
（保存 / 履歴 / 戻す）と、通常の Git クライアント相当の「詳細モード」を、
同じデータモデルの上に載せています。

- 対応 Unity: 2022.3 以上（Unity 6 を含む）
- 必要なもの: git 2.30 以上（自動インストールはしません）
- VRChat 固有の機能は別パッケージ `shiori-vrchat`（予定）に置きます。本パッケージは VRChat SDK に依存しません。

## インストール

Package Manager の "Add package from git URL" で次を指定します。

```
https://github.com/yaito3014/shiori.git
```

## 開発

リポジトリのルートがパッケージ本体です。検証用の Unity プロジェクトは
`DevProject~/<Unity バージョン>/` にあり、`file:../../..` でこのパッケージを参照しています。

```
# コンパイルのみ
"C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath DevProject~/2022.3 -logFile -

# EditMode テスト
"C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe" -batchmode -nographics -projectPath DevProject~/2022.3 -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -
```

## ライセンス

MIT
