#!/usr/bin/env bash
set -euo pipefail

echo "=== nexus6-se-workshop DevContainer セットアップ ==="

# ── 1. Azure Developer CLI (azd) ──────────────────────────────────────────────
echo "[1/4] Azure Developer CLI (azd) をインストール中..."
curl -fsSL https://aka.ms/install-azd.sh | bash
echo "  azd $(azd version) インストール完了"

# ── 2. GitHub Copilot CLI 拡張 (gh copilot) ──────────────────────────────────
echo "[2/4] GitHub Copilot CLI 拡張をインストール中..."
gh extension install github/gh-copilot 2>/dev/null || gh extension upgrade gh-copilot
echo "  gh copilot 拡張インストール完了"

# ── 3. .NET tools ─────────────────────────────────────────────────────────────
echo "[3/4] .NET グローバルツールをインストール中..."
# EF Core CLI（DemoDataGenerator のマイグレーション用）
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
echo "  dotnet-ef $(dotnet ef --version) インストール完了"

# ── 4. NuGet パッケージ復元（ソリューションが存在する場合） ──────────────────
echo "[4/4] NuGet パッケージを復元中..."
if [ -f "src/news-analysis-agent/NewsAnalysisAgent.sln" ]; then
  dotnet restore src/news-analysis-agent/NewsAnalysisAgent.sln
  echo "  news-analysis-agent 復元完了"
fi
if [ -f "src/DemoDataGenerator/DemoDataGenerator.csproj" ]; then
  dotnet restore src/DemoDataGenerator/DemoDataGenerator.csproj
  echo "  DemoDataGenerator 復元完了"
fi

echo ""
echo "=== セットアップ完了 ==="
echo "次のステップ:"
echo "  - Azure 認証: az login"
echo "  - GitHub 認証: gh auth login"
echo "  - azd 認証:   azd auth login"
echo "  - 接続文字列: src/*/appsettings.Development.json を作成して設定"
