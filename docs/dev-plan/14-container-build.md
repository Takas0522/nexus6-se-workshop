# 14. コンテナビルド

関連: [開発計画 README](README.md) | [実装ガイド](04-implementation-guide.md) | [Azure / M365 構成](09-azure-m365-configuration.md)

---

## Dockerfile 概要

`src/news-analysis-agent/Dockerfile` は .NET 10 のマルチステージ構成とする。

- build: `mcr.microsoft.com/dotnet/sdk:10.0`
- runtime: `mcr.microsoft.com/dotnet/aspnet:10.0`
- publish 対象: `src/NewsAnalysisAgent.Host/NewsAnalysisAgent.Host.csproj`
- 待受: `ASPNETCORE_URLS=http://+:8080` / `EXPOSE 8080`
- 実行ユーザー: 非 root の `app`

`.dockerignore` で `bin/`、`obj/`、`tests/`、`**/*.Development.json` を除外し、テストプロジェクトと開発用設定をイメージへ含めない。

---

## ビルドとタグ規約

ACR は `crnexus6swc`、リポジトリは `nexus6-hosted-agent` を使う。リリースタグと `latest` を同時に付与する。

```bash
az acr build \
  --registry crnexus6swc \
  --file src/news-analysis-agent/Dockerfile \
  --image nexus6-hosted-agent:0.1.0 \
  --image nexus6-hosted-agent:latest \
  src/news-analysis-agent
```

ローカル Docker が使える場合の検証コマンド:

```bash
docker build \
  -t nexus6-hosted-agent:local \
  -f src/news-analysis-agent/Dockerfile \
  src/news-analysis-agent
```

push は `az acr login -n crnexus6swc` 後、`crnexus6swc.azurecr.io/nexus6-hosted-agent:0.1.0` と `:latest` を登録する。

---

## タグ確認

```bash
az acr repository show-tags \
  -n crnexus6swc \
  --repository nexus6-hosted-agent \
  -o tsv
```

`0.1.0` と `latest` が表示されることを確認する。

---

## Smoke 確認

Docker が実行可能な環境では、DevUI を有効化して `/devui` を確認する。

```bash
docker run --rm -p 18080:8080 \
  -e DevUi__EnableManualTrigger=true \
  -e Storage__Account= \
  -e AzureMonitor__ConnectionString= \
  crnexus6swc.azurecr.io/nexus6-hosted-agent:0.1.0

curl -i http://localhost:18080/devui
```

`Storage__Account=` はローカル smoke 時だけ Queue polling を無効化するために指定する。Docker 実行または localhost のポート転送ができない環境では、ACR Tasks のビルド成功と image manifest / digest の取得を smoke 代替とする。

---

## ACA デプロイへの引き渡し

Azure Container Apps には ACR の `latest` または固定タグ `0.1.0` を渡す。

```bash
az containerapp up \
  --resource-group rg-nexus6-swc \
  --name ca-nexus6-hosted-agent \
  --image crnexus6swc.azurecr.io/nexus6-hosted-agent:0.1.0 \
  --environment cae-nexus6-swc \
  --location swedencentral \
  --ingress external --target-port 8080 \
  --system-assigned
```

ACR の admin user は有効化せず、Container Apps の Managed Identity に `AcrPull` を付与して pull する。
