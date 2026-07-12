# 12. News Portal デプロイ手順

関連: [開発計画 README](README.md) | [Azure / M365 必要構成まとめ](09-azure-m365-configuration.md) | [業務システム別ニュース分析シナリオ](../scenario/business-system-news-analysis-scenario.md)

---

## 目的

Demo 入力源である `src/news-portal/` の静的 HTML を Azure Storage Static Website に配置し、Foundry Trigger Agent が巡回できる公開 URL を確定する。

公開サイトは Foundry / Bing Grounding から fetch できるよう Public とする。ただし検索エンジン向けには `robots.txt` で `Disallow: /` を返し、Demo 用・Foundry Trigger Agent からの巡回専用として扱う。

---

## デプロイ先

| 項目 | 値 |
|---|---|
| Storage Account | `stnexus6portal1t2i` |
| ホスト方式 | Azure Storage Static Website |
| コンテナ | `$web` |
| Primary endpoint | `https://stnexus6portal1t2i.z1.web.core.windows.net/` |
| デプロイ元 | `src/news-portal/` |

---

## 公開 URL

| ページ | URL | 用途 |
|---|---|---|
| 記事一覧 | `https://stnexus6portal1t2i.z1.web.core.windows.net/` | Trigger Agent の入口 |
| 記事一覧（明示） | `https://stnexus6portal1t2i.z1.web.core.windows.net/index.html` | Trigger Agent の入口 |
| 為替急変 | `https://stnexus6portal1t2i.z1.web.core.windows.net/article-fx1.html` | シナリオ1 |
| 競合経済圏統合 | `https://stnexus6portal1t2i.z1.web.core.windows.net/article-comp1.html` | シナリオ2 |
| 日銀金融政策転換 | `https://stnexus6portal1t2i.z1.web.core.windows.net/article-boj1.html` | シナリオ3 |
| robots.txt | `https://stnexus6portal1t2i.z1.web.core.windows.net/robots.txt` | 検索エンジン除外 |
| sitemap.xml | `https://stnexus6portal1t2i.z1.web.core.windows.net/sitemap.xml` | Trigger Agent 巡回補助 |

---

## 初回デプロイ手順

```bash
az storage blob upload-batch \
  --account-name stnexus6portal1t2i \
  --source src/news-portal \
  --destination '$web' \
  --auth-mode login \
  --overwrite
```

デプロイ後、配置ファイルを確認する。

```bash
az storage blob list \
  --account-name stnexus6portal1t2i \
  --container-name '$web' \
  --auth-mode login \
  --query "[].name" \
  -o tsv | sort
```

期待値:

```text
article-boj1.html
article-comp1.html
article-fx1.html
index.html
robots.txt
sitemap.xml
```

Primary endpoint を取得する。

```bash
az storage account show \
  -n stnexus6portal1t2i \
  --query "primaryEndpoints.web" \
  -o tsv
```

公開状態を確認する。

```bash
curl -sI https://stnexus6portal1t2i.z1.web.core.windows.net/
curl -sI https://stnexus6portal1t2i.z1.web.core.windows.net/article-fx1.html
```

どちらも `HTTP/1.1 200 OK` を返すこと。

---

## 更新時の再デプロイ

HTML、`robots.txt`、`sitemap.xml` を更新した場合も同じ `upload-batch --overwrite` で再配置する。

```bash
az storage blob upload-batch \
  --account-name stnexus6portal1t2i \
  --source src/news-portal \
  --destination '$web' \
  --auth-mode login \
  --overwrite
```

再デプロイ後は `az storage blob list` と `curl -sI` でファイル存在と 200 応答を確認する。

---

## Foundry Trigger Agent 巡回設定例

| 項目 | 設定例 |
|---|---|
| Trigger Agent 名 | `news-portal-poller` |
| 起動方式 | Foundry Scheduled Trigger |
| 実行間隔 | Demo 中は手動 Run または 5〜15 分間隔 |
| 入口 URL | `https://stnexus6portal1t2i.z1.web.core.windows.net/` |
| 巡回補助 | `https://stnexus6portal1t2i.z1.web.core.windows.net/sitemap.xml` |
| 対象 URL パターン | `https://stnexus6portal1t2i.z1.web.core.windows.net/article-*.html` |
| 取得ツール | WebIQ または Grounding with Bing Search |
| 出力先 | Azure Storage Queue `news-analysis-jobs` |

Trigger Agent は入口 URL または `sitemap.xml` から記事 URL を取得し、未処理の記事本文と URL を `NewsAnalysisJob` JSON として Queue に enqueue する。News Portal 側に分析開始ボタンや JavaScript API 呼び出しは持たせない。
