# SessionGuard - Windows Session Manager Service

SessionGuard は、Windows OS 上で動作するバックグラウンドサービスで、ユーザーセッションを管理し、設定された条件に基づいて自動ログアウトを実行します。

## 機能

### 1. セッション監視
- Windows セッションのログイン時刻を自動取得
- セッション開始からの経過時間を追跡
- ユーザーがログアウト→ログインしても、サービスは継続稼働
- 新しいセッションで再び判定を開始

### 2. 複数時間帯別ログアウト
- 複数の時間帯を設定可能
- 設定された時間帯のいずれかに該当する場合、自動ログアウト
- 例：18:00～09:00（夜間）、12:00～13:00（昼休み）

### 3. 連続稼働時間制限
- 指定時間以上の稼働でログアウト
- デフォルト：1時間

### 4. 日次リセット
- 連続稼働時間は日付が変わると自動リセット
- 毎日の稼働開始時刻から計測

## システム要件

- **OS**: Windows 10/11 or Windows Server 2016 以上
- **.NET**: .NET 10 SDK

## インストール

### 前提条件

- **.NET 10 SDK** がインストールされていること
- **PowerShell** (管理者権限で実行)
- **Windows 10/11** または **Windows Server 2016** 以上

### 1. プロジェクトのビルド

プロジェクトをリリース構成でビルドします。

```powershell
# プロジェクトディレクトリへ移動
cd C:\wk\repos\SessionGuard\SessionGuard

# リリース構成でビルド
dotnet build -c Release
```

**ビルド結果：**
- ビルド成功後、`bin\Release\net10.0\` ディレクトリに実行可能ファイル（`SessionGuard.exe`）が生成されます
- 設定ファイル（`appsettings.json`）もこのディレクトリにコピーされます

**ビルドオプション：**
| オプション | 説明 |
|-----------|------|
| `-c Release` | リリース構成でビルド（最適化される）|
| `-c Debug` | デバッグ構成でビルド（デバッグ目的） |

**トラブルシューティング：**
- ビルド失敗時は、NuGet パッケージの復元を試してください：
  ```powershell
  dotnet restore
  dotnet build -c Release
  ```

### 2. Windows サービスとしてインストール

ビルドが完了したら、サービスをインストールして起動します。

```powershell
# リリース版ディレクトリへ移動
cd C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0

# サービスをインストール（このディレクトリから実行）
.\SessionGuard.exe --register-service

# サービスを起動
Start-Service -Name SessionGuard

# サービスの状態確認
Get-Service -Name SessionGuard
```

**サービス登録とは：**
- `--register-service` コマンドにより、SessionGuardが Windows サービスとして登録されます
- サービス名は `SessionGuard` です
- 登録後、Windows のサービス管理システムで自動的に起動・停止の管理が可能になります

**起動確認：**
出力例：
```
Status   Name               DisplayName
------   ----               -----------
Running  SessionGuard       SessionGuard
```

**設定ファイルの配置：**
- `appsettings.json` は `SessionGuard.exe` と同じディレクトリにあれば、自動的に読み込まれます
- サービス起動時に設定ファイルを確認して、設定に基づいいてログアウト判定を開始します

### 3. Windows サービスのアンインストール

サービスを削除する場合は、以下のコマンドを実行します。

```powershell
# サービスを停止
Stop-Service -Name SessionGuard

# サービスを削除（リリース版ディレクトリから実行）
cd C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0
.\SessionGuard.exe --unregister-service
```

**代替方法**（`--unregister-service` がない場合）：

```powershell
sc.exe delete SessionGuard
```

**削除確認：**
```powershell
Get-Service -Name SessionGuard
```
エラーが出れば、削除完了です。

## 動作確認

サービスが正常に動作しているか確認するには、Windows イベントログを確認します。

```powershell
# SessionGuard の最新ログを確認（最新10件）
Get-EventLog -LogName Application -Source "SessionGuard" -Newest 10 | Format-Table -AutoSize

# より詳細なメッセージを表示
Get-EventLog -LogName Application -Source "SessionGuard" -Newest 5 | ForEach-Object { "$($_.TimeGenerated) | $($_.Message)" }
```

**ログの見方：**
- **Information** レベル：サービスの起動・セッション状態の通常ログ
- **Warning** レベル：ログアウト条件が検出された
- **Error** レベル：ログアウト実行時のエラー

**トラブルシューティング：**
- ログに「Current time is within logout time window」が表示されたら、設定時間帯内にいます
- ログに「Logout condition detected」が表示されたら、ログアウト実行が試みられました

## 設定

### appsettings.json

プロジェクトルートの `appsettings.json` で以下の設定を可能にしています:

```json
{
  "SessionConfig": {
    "LogoutTimeWindows": [
      {
        "StartTime": "18:00",
        "EndTime": "09:00",
        "Description": "夜間・早朝の営業外時間"
      },
      {
        "StartTime": "12:00",
        "EndTime": "13:00",
        "Description": "昼休み時間"
      }
    ],
    "MaxContinuousUptime": 1,
    "EnableLogout": true,
    "CheckInterval": 5
  }
}
```

#### 設定項目の説明

| 項目 | 型 | 説明 |
|------|-----|------|
| `LogoutTimeWindows` | array | ログアウト対象の時間帯リスト（複数設定可能） |
| `LogoutTimeWindows[].StartTime` | string | 時間帯開始時刻 (HH:mm形式) |
| `LogoutTimeWindows[].EndTime` | string | 時間帯終了時刻 (HH:mm形式) |
| `LogoutTimeWindows[].Description` | string | 時間帯の説明 (ログ出力用) |
| `MaxContinuousUptime` | int | 最大連続稼働時間（時間単位） |
| `EnableLogout` | bool | ログアウト機能を有効にするか |
| `CheckInterval` | int | ログアウト条件の確認間隔（秒単位） |

#### 複数時間帯設定の例

```json
{
  "SessionConfig": {
    "LogoutTimeWindows": [
      {
        "StartTime": "18:00",
        "EndTime": "09:00",
        "Description": "夜間・早朝（18:00～09:00）"
      },
      {
        "StartTime": "12:00",
        "EndTime": "13:00",
        "Description": "昼休み（12:00～13:00）"
      },
      {
        "StartTime": "15:30",
        "EndTime": "15:45",
        "Description": "休憩時間（15:30～15:45）"
      }
    ],
    "MaxContinuousUptime": 1,
    "EnableLogout": true,
    "CheckInterval": 60
  }
}
```

この設定では以下の時間帯にログアウトされます：
- 18:00 ～ 09:00（夜間・早朝）
- 12:00 ～ 13:00（昼休み）
- 15:30 ～ 15:45（休憩時間）

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
    "LogoutTimeWindows": [
      {
        "StartTime": "20:00",
        "EndTime": "06:00",
        "Description": "営業外時間（20:00～06:00）"
      },
      {
        "StartTime": "11:30",
        "EndTime": "12:30",
        "Description": "昼休み（11:30～12:30）"
      }
    ],
    "MaxContinuousUptime": 10,
    "EnableLogout": true,
    "CheckInterval": 120
  }
}
```

この設定では：
- **動作時間**: 06:00 ～ 20:00（上記の時間帯以外）
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
