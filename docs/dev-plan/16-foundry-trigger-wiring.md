# 16. Foundry Trigger Wiring / Queue E2E 記録

関連: [開発計画 README](README.md) | [News Portal デプロイ](12-news-portal-deployment.md) | [Foundry ランタイム構成](13-foundry-runtime-config.md) | [ACA デプロイ](15-aca-deployment.md)

---

## 目的

Foundry Trigger Agent が未作成の状態でも、News Portal 記事を Azure Storage Queue `news-analysis-jobs` に投入し、ACA `ca-nexus6-hosted-agent` の `QueueBackgroundService` が dequeue して Workflow を実行できることを確認する。

---

## 事前 RBAC 確認

現在の `az login` principal は Storage Account `stnexus6skill1t2i` に対して subscription `Owner` のみで、Queue データプレーン権限がなかったため、検証用に Queue ロールを付与した。

```bash
ACCOUNT=stnexus6skill1t2i
ST_ID="$(az storage account show -n "$ACCOUNT" --query id -o tsv)"
ME_ID="$(az ad signed-in-user show --query id -o tsv)"

az role assignment list \
  --assignee "$ME_ID" \
  --scope "$ST_ID" \
  --include-inherited \
  --query "[].{role:roleDefinitionName,scope:scope}" \
  -o table

az role assignment create \
  --assignee-object-id "$ME_ID" \
  --assignee-principal-type User \
  --role "Storage Queue Data Message Sender" \
  --scope "$ST_ID"

# az storage message put / peek のデータプレーン操作で Contributor が要求されたため追加。
az role assignment create \
  --assignee-object-id "$ME_ID" \
  --assignee-principal-type User \
  --role "Storage Queue Data Contributor" \
  --scope "$ST_ID"
```

確認結果:

```text
principalId=9eb61672-cf60-4e1f-aad9-529e69ae48ad
Role                               Scope
---------------------------------  ------------------------------------------------------------------------------
Owner                              /subscriptions/b604add2-86ec-47d6-bd23-672ca63c216a
Storage Queue Data Message Sender  .../storageAccounts/stnexus6skill1t2i
Storage Queue Data Contributor     .../storageAccounts/stnexus6skill1t2i
```

---

## 手動 enqueue 手順

### 1. ACA を起動状態にする

ACA は min replicas `0` のため、先に `/health` を叩いて起動させる。

```bash
FQDN="$(az containerapp show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query properties.configuration.ingress.fqdn \
  -o tsv)"

curl -sS -o /dev/null -w '%{http_code}\n' "https://$FQDN/health"
```

確認結果: `200`

### 2. News Portal から本文抽出し JSON 化する

`article-fx1.html` には `<article>` タグがないため、`.article-wrap` 周辺を抽出し HTML タグを除去した。

```bash
mkdir -p logs
python3 - <<'PY' > logs/n-foundry-trigger-wire-payload.json
import html, json, re, urllib.request

url = 'https://stnexus6portal1t2i.z1.web.core.windows.net/article-fx1.html'
raw = urllib.request.urlopen(url, timeout=20).read().decode('utf-8')
match = re.search(r'<div class="article-wrap">(.*?)</div>\s*</div>\s*<!-- 関連記事 -->', raw, re.S)
content = match.group(1) if match else raw
content = re.sub(r'<script.*?</script>|<style.*?</style>', ' ', content, flags=re.S | re.I)
text = html.unescape(re.sub(r'<[^>]+>', ' ', content))
text = re.sub(r'\s+', ' ', text).strip()

payload = {
    'originalNewsText': text,
    'sourceUrl': url,
    'publishedAt': '2026-06-27T10:32:00+09:00',
}
print(json.dumps(payload, ensure_ascii=False, separators=(',', ':')))
PY
```

payload preview:

```text
{"originalNewsText":"【速報】為替・緊急 円が一時158円台に急落 地政学リスクで年初来安値を更新、輸入コスト上昇懸念が広がる 📰 NEXUS経済編集部 🕐 2026年6月27日 10:32 ...","sourceUrl":"https://stnexus6portal1t2i.z1.web.core.windows.net/article-fx1.html","publishedAt":"2026-06-27T10:32:00+09:00"}
payload_bytes=5469
```

### 3. Base64 化して Queue に投入する

```bash
az storage message put \
  --queue-name news-analysis-jobs \
  --account-name stnexus6skill1t2i \
  --auth-mode login \
  --content "$(base64 -w 0 logs/n-foundry-trigger-wire-payload.json)" \
  -o json
```

投入結果:

```json
{
  "id": "d8dd5500-9114-43ac-8aad-32c85798ee27",
  "insertionTime": "2026-06-27T09:58:51+00:00",
  "expirationTime": "2026-07-04T09:58:51+00:00",
  "timeNextVisible": "2026-06-27T09:58:51+00:00"
}
```

---

## ACA ログ確認手順

```bash
az containerapp logs show \
  -n ca-nexus6-hosted-agent \
  -g rg-nexus6-swc \
  --tail 300 \
  --follow false \
  > logs/n-foundry-trigger-wire-aca-logs-full.txt

FQDN="$(az containerapp show \
  -g rg-nexus6-swc \
  -n ca-nexus6-hosted-agent \
  --query properties.configuration.ingress.fqdn \
  -o tsv)"

curl -sS "https://$FQDN/devui/logs" \
  > logs/n-foundry-trigger-wire-devui-logs.json

az storage message peek \
  --queue-name news-analysis-jobs \
  --account-name stnexus6skill1t2i \
  --auth-mode login \
  --num-messages 1 \
  --query 'length(@)' \
  -o tsv
```

確認結果:

```text
Queue peek length: 0
```

`/devui/logs` の最新 Workflow 実行結果:

```text
runId=509cd62c-027f-409f-8a56-d92675182217
originalNewsText.prefix=【速報】為替・緊急 円が一時158円台に急落 地政学リスクで年初来安値を更新、輸入コスト上昇懸念が広がる ...
web-research succeeded=True started=2026-06-27T09:58:52.9826512+00:00 completed=2026-06-27T09:58:53.0973625+00:00
business-impact succeeded=True started=2026-06-27T09:58:53.0973936+00:00 completed=2026-06-27T09:58:53.1768014+00:00
division-recommend-mobile succeeded=True started=2026-06-27T09:58:53.1768528+00:00 completed=2026-06-27T09:58:53.2529536+00:00
division-recommend-fintech succeeded=True started=2026-06-27T09:58:53.1780433+00:00 completed=2026-06-27T09:58:53.3139706+00:00
division-recommend-ecommerce succeeded=True started=2026-06-27T09:58:53.1777742+00:00 completed=2026-06-27T09:58:53.3139706+00:00
notification succeeded=True started=2026-06-27T09:58:53.3140224+00:00 completed=2026-06-27T09:58:53.3983657+00:00
```

ACA ログ抜粋:

```text
2026-06-27T09:58:53.177513+00:00 DivisionRecommendAgent invoked for Mobile
2026-06-27T09:58:53.1783723+00:00 DivisionRecommendAgent invoked for Ecommerce
2026-06-27T09:58:53.1786973+00:00 DivisionRecommendAgent invoked for Fintech
2026-06-27T09:58:53.3146924+00:00 Workflow step division-recommend-ecommerce completed in 136 ms. Succeeded: True
2026-06-27T09:58:53.3146949+00:00 Workflow step division-recommend-fintech completed in 135 ms. Succeeded: True
2026-06-27T09:58:53.3146996+00:00 Workflow step notification started
2026-06-27T09:58:53.3147021+00:00 NotificationAgent sending 3 Teams notifications
2026-06-27T09:58:53.3984376+00:00 Workflow step notification completed in 84 ms. Succeeded: True
```

補足:

- Queue 投入から Workflow 開始まで約 1 秒、5 分以内に消費完了。
- `Teams Workflows URL is not configured ... using mock fallback` の後、mock fallback が `/app/logs` へ書き込もうとして `Permission denied` になったが、`NotificationAgent` は例外を捕捉して継続し、Workflow step としては `Succeeded: True` で完了した。
- 手動 enqueue E2E が成功したため、`/devui/run` 直接 POST による失敗比較は未実施。

---

## Foundry Trigger Agent 自動化の追加試行

`https://ai.azure.com` 向け token を取得して REST API surface を確認した。

```bash
TOKEN="$(az account get-access-token \
  --resource https://ai.azure.com \
  --query accessToken \
  -o tsv)"
ENDPOINT='https://fd-partneriq.services.ai.azure.com/api/projects/proj-PartnerIQ'

curl -sS \
  -H "Authorization: Bearer $TOKEN" \
  "$ENDPOINT/schedules?api-version=2025-10-15-preview"

curl -sS \
  -H "Authorization: Bearer $TOKEN" \
  "$ENDPOINT/assistants?api-version=2025-10-15-preview"

curl -sS \
  -H "Authorization: Bearer $TOKEN" \
  "$ENDPOINT/agents?api-version=2025-10-15-preview"
```

結果:

```text
GET /schedules?api-version=2025-10-15-preview -> 200
{"value":[]}

GET /assistants?api-version=2025-10-15-preview -> 200
{"object":"list","data":[],"first_id":null,"last_id":null,"has_more":false}

GET /agents?api-version=2025-10-15-preview -> 400
Unsupported api-version '2025-10-15-preview'. Supported values: ... 2025-11-15-preview, 1.0.
```

判断:

- Schedule 一覧 API と Assistant 一覧 API は参照できた。
- WebIQ / Queue 書き込み Tool を構成し、Scheduled Trigger と Agent / Assistant を結線する安定した作成 API は確認できなかった。
- 破壊的な半端リソース作成を避けるため、Trigger Agent 作成は `13-foundry-runtime-config.md` の手動引継ぎを維持する。
- `agent_id` は未作成のため追記なし。

---

## S1〜S3 シナリオ検証用 enqueue 例

3 記事を順に Queue に投入する検証用スニペット。本文抽出は `.article-wrap` を優先し、取れない場合は HTML 全文からタグ除去する。

```bash
set -euo pipefail
mkdir -p logs

BASE_URL='https://stnexus6portal1t2i.z1.web.core.windows.net'
QUEUE='news-analysis-jobs'
ACCOUNT='stnexus6skill1t2i'

enqueue_article() {
  local path="$1"
  local published_at="$2"
  local out="logs/enqueue-${path%.html}.json"

  ARTICLE_PATH="$path" \
  PUBLISHED_AT="$published_at" \
  BASE_URL="$BASE_URL" \
  python3 - <<'PY' > "$out"
import html, json, os, re, urllib.request
base = os.environ['BASE_URL'].rstrip('/')
path = os.environ['ARTICLE_PATH']
url = f'{base}/{path}'
raw = urllib.request.urlopen(url, timeout=20).read().decode('utf-8')
match = re.search(r'<div class="article-wrap">(.*?)</div>\s*</div>\s*<!-- 関連記事 -->', raw, re.S)
content = match.group(1) if match else raw
content = re.sub(r'<script.*?</script>|<style.*?</style>', ' ', content, flags=re.S | re.I)
text = html.unescape(re.sub(r'<[^>]+>', ' ', content))
text = re.sub(r'\s+', ' ', text).strip()
print(json.dumps({
    'originalNewsText': text,
    'sourceUrl': url,
    'publishedAt': os.environ['PUBLISHED_AT'],
}, ensure_ascii=False, separators=(',', ':')))
PY

  az storage message put \
    --queue-name "$QUEUE" \
    --account-name "$ACCOUNT" \
    --auth-mode login \
    --content "$(base64 -w 0 "$out")"
}

enqueue_article 'article-fx1.html' '2026-06-27T10:32:00+09:00'
enqueue_article 'article-comp1.html' '2026-06-27T09:15:00+09:00'
enqueue_article 'article-boj1.html' '2026-06-27T08:00:00+09:00'
```
