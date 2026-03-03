# SessionGuard - Windows Session Manager Service

SessionGuard は、Windows OS 上で動作するバックグラウンドサービスで、ユーザーセッションを管理し、設定された条件に基づいて自動ログアウトを実行します。

## 機能

### 1. 起動時刻管理
- サービス起動時刻を取得・保持
- セッション開始時刻からの経過時間を追跡

### 2. 時間帯別ログアウト
- 設定された時間帯外のアクセスを検出
- 例: 22:00 ～ 08:00 の時間帯はログアウト対象

### 3. 連続稼働時間制限
- 設定された稼働時間を超過した場合はログアウト
- デフォルト: 8時間

### 4. 日次リセット
- 連続稼働時間は日付が変更される度にリセット
- 毎日の稼働開始時刻から計算

## システム要件

- **OS**: Windows 10/11 or Windows Server 2016 以上
- **.NET**: .NET 10 SDK

## インストール

### 1. プロジェクトのビルド

```powershell
cd C:\wk\repos\SessionGuard\SessionGuard
dotnet build -c Release
```

### 2. Windows サービスとしてインストール

```powershell
# サービスをインストール
sc.exe create SessionGuard binPath="C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0\SessionGuard.exe"

# サービスを起動
net start SessionGuard

# サービスの状態確認
sc.exe query SessionGuard
```

### 3. Windows サービスのアンインストール

```powershell
# サービスを停止
net stop SessionGuard

# サービスを削除
sc.exe delete SessionGuard
```

## 設定

### appsettings.json

プロジェクトルートの `appsettings.json` で以下の設定を可能にしています:

```json
{
  "SessionConfig": {
    "LogoutStartTime": "18:00",
    "LogoutEndTime": "09:00",
    "MaxContinuousUptime": 1,
    "EnableLogout": true,
    "CheckInterval": 60
  }
}
```

#### 設定項目の説明

| 項目 | 型 | 現在値 | 説明 |
|------|-----|--------|------|
| `LogoutStartTime` | string | "18:00" | ログアウト時間帯の開始時刻 (HH:mm形式) |
| `LogoutEndTime` | string | "09:00" | ログアウト時間帯の終了時刻 (HH:mm形式) |
| `MaxContinuousUptime` | int | 1 | 最大連続稼働時間（時間単位） |
| `EnableLogout` | bool | true | ログアウト機能を有効にするか |
| `CheckInterval` | int | 60 | ログアウト条件の確認間隔（秒単位） |

#### 設定値のリアルタイム反映

**重要**: このアプリケーションは `IOptionsMonitor` を使用しており、`appsettings.json` を編集すると、**サービスを再起動せずに自動的に新しい設定が反映されます**。

##### 動作例

1. サービスが起動中の状態で `appsettings.json` を編集
2. ファイルを保存
3. 数秒以内に新しい設定が自動的に読み込まれる
4. イベントビューアでログ出力を確認: `"Configuration changed. Updating SessionManager..."`

##### 注意事項

- ファイルの編集は **JSON 形式が正しい状態で保存**してください
- 形式エラーがあると読み込みが失敗します
- ファイル監視には若干のタイムラグ（数秒程度）があります

### 例: 異なる設定

```json
{
  "SessionConfig": {
    "LogoutStartTime": "20:00",
    "LogoutEndTime": "06:00",
    "MaxContinuousUptime": 10,
    "EnableLogout": true,
    "CheckInterval": 120
  }
}
```

この設定では：
- **動作時間**: 06:00 ～ 20:00（20:00 ～ 06:00 はログアウト時間帯）
- **最大稼働**: 10時間以上稼働していればログアウト
- **確認間隔**: 120秒（2分）ごとに条件をチェック

## ログ

サービスはログファイルを Windows Event Viewer に出力します。

### ログの確認

```powershell
# Event Viewer を開く
eventvwr.msc

# 以下の場所でログを確認:
# Windows ログ > Application
```

### ログレベル

- **Information**: 通常のサービス操作（起動、停止、ステータス）
- **Warning**: ログアウト条件の検出
- **Error**: エラーの発生
- **Critical**: ログアウト実行時

## プロジェクト構造

```
SessionGuard/
├── SessionGuard.csproj          # プロジェクトファイル
├── Program.cs                   # アプリケーション エントリポイント
├── Worker.cs                    # バックグラウンドサービス実装
├── SessionConfig.cs             # セッション設定クラス
├── SessionInfo.cs               # セッション情報クラス
├── SessionManager.cs            # セッション管理ロジック
├── LogoutHandler.cs             # ログアウト処理実装
├── appsettings.json             # アプリケーション設定
├── appsettings.Development.json # 開発環境設定
└── Properties/
    └── launchSettings.json      # 起動設定
```

## 開発

### ビルド

```powershell
cd C:\wk\repos\SessionGuard\SessionGuard
dotnet build
```

### 実行（デバッグモード）

```powershell
cd C:\wk\repos\SessionGuard\SessionGuard
dotnet run
```

### テスト

```powershell
# ユニットテストプロジェクト（別途作成が必要）
dotnet test
```

## トラブルシューティング

### サービスが起動しない場合

1. **ビルドの確認**
   ```powershell
   dotnet build -c Release
   ```

2. **設定ファイルの確認**
   - `appsettings.json` が正しい形式であることを確認

3. **イベントビューアの確認**
   - Windows Event Viewer でエラーメッセージを確認

### ログアウトが実行されない場合

1. **EnableLogout 設定を確認**
   ```json
   "EnableLogout": true
   ```

2. **時間設定を確認**
   - `LogoutStartTime` と `LogoutEndTime` の値を確認

3. **CheckInterval の調整**
   - チェック間隔を短くして、反応時間を改善

## ライセンス

このプロジェクトはプライベートプロジェクトです。

## サポート

問題が発生した場合は、以下の方法で対応してください:

1. イベントビューアで詳細なエラーメッセージを確認
2. `appsettings.json` の設定を見直す
3. サービスを再起動する

## 変更履歴

### v1.0.0 (Initial Release)
- 基本的なセッション管理機能の実装
- 時間帯別ログアウト機能
- 稼働時間制限機能
- 日次リセット機能
