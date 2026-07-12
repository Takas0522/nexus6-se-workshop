# News Portal Trigger Function

`news-analysis-jobs` キューへ記事本文を投入する Azure Functions（Timer Trigger）です。

## 実行タイミング

- `DailyRunCron`（NCRONTAB）で指定
- JST 14:55 を毎日実行する場合: `0 55 5 * * *`（UTC 05:55）

## 必須設定

- `Storage__Account`（例: `stnexus6skill1t2i`）
- `Storage__QueueName`（既定: `news-analysis-jobs`）
- `NewsPortal__BaseUrl`
- `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`
- Queue 送信は `Base64` エンコード前提

## ローカル

```bash
cd src/news-trigger-function
cp local.settings.json.example local.settings.json
dotnet build
```

## Azure デプロイ（zip deploy）

```bash
cd src/news-trigger-function
dotnet publish -c Release -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
az functionapp deployment source config-zip \
  -g rg-nexus6-swc \
  -n <FUNCTION_APP_NAME> \
  --src publish.zip
```
