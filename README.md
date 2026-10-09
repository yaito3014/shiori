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

リポジトリのルートがパッケージ本体です。検証用の Unity プロジェクトはリポジトリの外に置きます
（中に置くと、ウィザードがリポジトリの入れ子を検出して止まるため）。

```
# 検証用プロジェクトを作る（既定: ../shiori-dev/<major.minor>）
pwsh Tools~/New-DevProject.ps1 -UnityVersion 2022.3.22f1
pwsh Tools~/New-DevProject.ps1 -UnityVersion 6000.6.0f1

# EditMode テストを回して結果を要約する
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 6000.6.0f1

# コンパイルだけ
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1 -CompileOnly
```

CI（GitHub Actions）は同じスクリプトでプロジェクトを生成してから GameCI でテストを回します。

## ライセンス

MIT
