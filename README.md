# Pomodoro Blocker (Windows)

Windows向けのポモドーロタイマーです。**作業フェーズ中は指定したブラウザ/ゲーム系プロセスを毎秒監視して終了**します。

## 仕様
- 作業時間 / 休憩時間を分単位で設定
- `Start / Pause / Reset`
- 作業フェーズ中のみブロック有効
- 休憩フェーズではブロック無効
- ブロック対象プロセス一覧を画面表示

## 実行方法
```bash
dotnet build
# Windows環境で
 dotnet run
```

> 一部のプロセス終了には管理者権限が必要な場合があります。

## ブロック対象
`MainForm.cs` の `_blockedProcessNames` を編集してください。

