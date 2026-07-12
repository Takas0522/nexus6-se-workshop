using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EnvironmentSetup.App.Models;
using EnvironmentSetup.App.Services;

namespace EnvironmentSetup.App.Steps;

/// <summary>
/// ステップ8: オントロジー作成 - データテーブル構成に基づき Fabric IQ Ontology を作成
/// </summary>
public class Step08OntologyCreation : ISetupStep
{
    private readonly AzureCliWrapper _az;
    private const string FabricResource = "https://api.fabric.microsoft.com";

    public int StepNumber => 8;
    public string Name => "オントロジー作成";

    public Step08OntologyCreation(AzureCliWrapper az)
    {
        _az = az;
    }

    public async Task ExecuteAsync(SetupState state, CancellationToken ct = default)
    {
        var azure = state.Azure;
        if (azure != null && !azure.FabricAvailable)
        {
            Console.WriteLine("  ⏭️  Fabric が無効のためスキップします (FabricAvailable=false)");
            return;
        }

        var analysis = state.Analysis
            ?? throw new InvalidOperationException("分析が未完了です。Step 2 を先に実行してください。");

        Console.WriteLine("  Fabric IQ オントロジーを作成中...\n");

        // 1. ワークスペースとLakehouseを特定
        var (wsId, lhId, lhName) = await ResolveFabricTargetsAsync(state);
        Console.WriteLine($"    ワークスペース: {wsId}");
        Console.WriteLine($"    Lakehouse: {lhName} ({lhId})\n");

        // 2. 既存オントロジーの確認
        var existingOntoId = await FindExistingOntologyAsync(wsId, state);
        if (!string.IsNullOrEmpty(existingOntoId))
        {
            Console.WriteLine($"  ℹ️  既存オントロジーが見つかりました (ID: {existingOntoId})");
            Console.WriteLine("      既存定義を更新します。\n");
        }

        // 3. テーブル定義からオントロジー構造を構築
        var ontologyDef = BuildOntologyDefinition(analysis.Tables, wsId, lhId);
        Console.WriteLine($"    EntityType 数: {ontologyDef.EntityTypes.Count}");
        Console.WriteLine($"    RelationshipType 数: {ontologyDef.Relationships.Count}\n");

        // 4. オントロジーの作成 or 更新
        try
        {
            string ontoId;
            if (string.IsNullOrEmpty(existingOntoId))
            {
                ontoId = await CreateOntologyAsync(wsId, ontologyDef, ct);
                Console.WriteLine($"    ✓ オントロジー作成完了 (ID: {ontoId})");
            }
            else
            {
                ontoId = existingOntoId;
                await UpdateOntologyDefinitionAsync(wsId, ontoId, ontologyDef, ct);
                Console.WriteLine($"    ✓ オントロジー更新完了 (ID: {ontoId})");
            }

            // 5. 結果をstateに保存
            state.Ontology = new OntologyResult
            {
                OntologyId = ontoId,
                WorkspaceId = wsId,
                LakehouseId = lhId,
                EntityTypeCount = ontologyDef.EntityTypes.Count,
                RelationshipCount = ontologyDef.Relationships.Count
            };

            Console.WriteLine($"\n  ✓ オントロジー作成完了");
            Console.WriteLine($"    Entity Types: {string.Join(", ", ontologyDef.EntityTypes.Select(e => e.Name))}");
        }
        catch (Exception ex) when (
            ex is TimeoutException ||
            (ex is InvalidOperationException ioe && (
                ioe.Message.Contains("FeatureNotAvailable") ||
                ioe.Message.Contains("Forbidden") ||
                ioe.Message.Contains("タイムアウト") ||
                ioe.Message.Contains("exit 1"))))
        {
            Console.WriteLine("  ⚠️ Ontology 機能がこのテナント/容量では利用できません");
            Console.WriteLine($"     ({ex.Message[..Math.Min(150, ex.Message.Length)]})");
            Console.WriteLine("     この手順はスキップされます。F64以上のSKUが必要な場合があります。");
            state.Ontology = new OntologyResult
            {
                OntologyId = "",
                WorkspaceId = wsId,
                LakehouseId = lhId,
                EntityTypeCount = ontologyDef.EntityTypes.Count,
                RelationshipCount = ontologyDef.Relationships.Count
            };
        }
    }

    private async Task<(string wsId, string lhId, string lhName)> ResolveFabricTargetsAsync(SetupState state)
    {
        // ワークスペース一覧取得
        var wsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces\" --resource \"{FabricResource}\"",
            silent: true);
        var wsDoc = JsonDocument.Parse(wsJson);
        var workspaces = wsDoc.RootElement.GetProperty("value");

        // state から特定 or 最初のワークスペースを使用
        string wsId;
        if (!string.IsNullOrEmpty(state.Ontology?.WorkspaceId))
        {
            wsId = state.Ontology.WorkspaceId;
        }
        else if (!string.IsNullOrEmpty(state.Medallion?.WorkspaceId))
        {
            wsId = state.Medallion.WorkspaceId;
        }
        else
        {
            // "ws-nexus6-medallion" を優先、なければ "My workspace" 以外の最初
            wsId = workspaces.EnumerateArray()
                .Where(w => w.GetProperty("displayName").GetString() == "ws-nexus6-medallion")
                .Select(w => w.GetProperty("id").GetString()!)
                .FirstOrDefault()
                ?? workspaces.EnumerateArray()
                    .Where(w => w.GetProperty("displayName").GetString() != "My workspace")
                    .Select(w => w.GetProperty("id").GetString()!)
                    .FirstOrDefault()
                ?? throw new InvalidOperationException("Fabric ワークスペースが見つかりません。");
        }

        // ワークスペース内のLakehouseを検索
        var itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Lakehouse\" --resource \"{FabricResource}\"",
            silent: true);
        var itemsDoc = JsonDocument.Parse(itemsJson);
        var lakehouses = itemsDoc.RootElement.GetProperty("value");

        // 最初のLakehouseを使用 (将来的には state.Deployment から特定)
        var lh = lakehouses.EnumerateArray().FirstOrDefault();
        if (lh.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("ワークスペース内に Lakehouse が見つかりません。");

        var lhId = lh.GetProperty("id").GetString()!;
        var lhName = lh.GetProperty("displayName").GetString()!;

        return (wsId, lhId, lhName);
    }

    private async Task<string?> FindExistingOntologyAsync(string wsId, SetupState state)
    {
        if (!string.IsNullOrEmpty(state.Ontology?.OntologyId))
            return state.Ontology.OntologyId;

        var itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Ontology\" --resource \"{FabricResource}\"",
            silent: true);
        var itemsDoc = JsonDocument.Parse(itemsJson);
        var ontologies = itemsDoc.RootElement.GetProperty("value");

        // 既存オントロジーがあればそのIDを返す
        var first = ontologies.EnumerateArray().FirstOrDefault();
        return first.ValueKind != JsonValueKind.Undefined
            ? first.GetProperty("id").GetString()
            : null;
    }

    private static OntologyDefinition BuildOntologyDefinition(
        List<TableDefinition> tables, string wsId, string lhId)
    {
        var def = new OntologyDefinition();
        long nextId = 100000000000001;

        foreach (var table in tables)
        {
            var entityType = new OntologyEntityType
            {
                Id = (nextId++).ToString(),
                Name = ToPascalCase(table.TableName),
                TableName = table.TableName
            };

            long propId = nextId;
            ColumnDefinition? keyColumn = null;

            foreach (var col in table.Columns)
            {
                var prop = new OntologyProperty
                {
                    Id = (propId++).ToString(),
                    Name = col.Name,
                    ValueType = MapColumnTypeToValueType(col.Type)
                };
                entityType.Properties.Add(prop);

                // 最初のカラム or "_id" が付くカラムをキーとする
                if (keyColumn == null || col.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
                    keyColumn = col;
            }

            nextId = propId + 100; // IDの衝突を避ける

            // キープロパティの設定
            var keyProp = entityType.Properties
                .FirstOrDefault(p => p.Name == keyColumn?.Name)
                ?? entityType.Properties.First();
            entityType.KeyPropertyId = keyProp.Id;
            entityType.DisplayNamePropertyId = entityType.Properties.Count > 1
                ? entityType.Properties[1].Id
                : keyProp.Id;

            // DataBinding
            entityType.Binding = new OntologyDataBinding
            {
                Id = Guid.NewGuid().ToString(),
                WorkspaceId = wsId,
                LakehouseId = lhId,
                SourceTableName = table.TableName,
                PropertyBindings = entityType.Properties
                    .Select(p => new PropertyBinding { SourceColumnName = p.Name, TargetPropertyId = p.Id })
                    .ToList()
            };

            def.EntityTypes.Add(entityType);
        }

        // テーブル間の FK 関係を推定してリレーションシップを作成
        BuildRelationships(def);

        return def;
    }

    private static void BuildRelationships(OntologyDefinition def)
    {
        long relId = 900000000000001;

        foreach (var entity in def.EntityTypes)
        {
            foreach (var prop in entity.Properties)
            {
                // _id で終わるプロパティで、同名テーブルがあればリレーション作成
                if (!prop.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var targetTableName = prop.Name[..^3]; // "_id" を除去
                var targetEntity = def.EntityTypes
                    .FirstOrDefault(e =>
                        string.Equals(e.TableName, targetTableName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.TableName, targetTableName + "s", StringComparison.OrdinalIgnoreCase));

                if (targetEntity == null || targetEntity == entity)
                    continue;

                // 同じペアのリレーションが既にあれば重複しない
                if (def.Relationships.Any(r =>
                    r.SourceEntityTypeId == entity.Id && r.TargetEntityTypeId == targetEntity.Id))
                    continue;

                def.Relationships.Add(new OntologyRelationship
                {
                    Id = (relId++).ToString(),
                    Name = $"{entity.Name}To{targetEntity.Name}",
                    SourceEntityTypeId = entity.Id,
                    TargetEntityTypeId = targetEntity.Id
                });
            }
        }
    }

    private async Task<string> CreateOntologyAsync(
        string wsId, OntologyDefinition ontologyDef, CancellationToken ct)
    {
        var ontoName = "DemoOntology";
        var parts = BuildDefinitionParts(ontologyDef, ontoName);

        var envelope = new JsonObject
        {
            ["displayName"] = ontoName,
            ["type"] = "Ontology",
            ["definition"] = new JsonObject
            {
                ["parts"] = parts
            }
        };

        var envelopePath = Path.GetFullPath("./output/ontology_create.json");
        Directory.CreateDirectory(Path.GetDirectoryName(envelopePath)!);
        await File.WriteAllTextAsync(envelopePath, envelope.ToJsonString(), ct);

        // Create Item (LRO)
        var opId = await ExecuteFabricLroAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items\" " +
            $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
            $"--body @{envelopePath} --verbose");

        if (opId != null)
            await PollLroAsync(opId, ct);

        // 作成されたオントロジーのIDを取得
        var itemsJson = await _az.RunAsync(
            $"rest --method get --url \"{FabricResource}/v1/workspaces/{wsId}/items?type=Ontology\" --resource \"{FabricResource}\"",
            silent: true);
        var itemsDoc = JsonDocument.Parse(itemsJson);
        var ontoItem = itemsDoc.RootElement.GetProperty("value").EnumerateArray()
            .FirstOrDefault(i => i.GetProperty("displayName").GetString() == ontoName);

        return ontoItem.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("オントロジーの作成後にアイテムが見つかりません。");
    }

    private async Task UpdateOntologyDefinitionAsync(
        string wsId, string ontoId, OntologyDefinition ontologyDef, CancellationToken ct)
    {
        var parts = BuildDefinitionParts(ontologyDef, "DemoOntology");

        var envelope = new JsonObject
        {
            ["definition"] = new JsonObject
            {
                ["parts"] = parts
            }
        };

        var envelopePath = Path.GetFullPath("./output/ontology_update.json");
        Directory.CreateDirectory(Path.GetDirectoryName(envelopePath)!);
        await File.WriteAllTextAsync(envelopePath, envelope.ToJsonString(), ct);

        var opId = await ExecuteFabricLroAsync(
            $"rest --method POST --url \"{FabricResource}/v1/workspaces/{wsId}/items/{ontoId}/updateDefinition\" " +
            $"--resource \"{FabricResource}\" --headers \"Content-Type=application/json\" " +
            $"--body @{envelopePath} --verbose");

        if (opId != null)
            await PollLroAsync(opId, ct);
    }

    private static JsonArray BuildDefinitionParts(OntologyDefinition def, string displayName)
    {
        var parts = new JsonArray();

        // .platform
        var platform = new JsonObject
        {
            ["metadata"] = new JsonObject
            {
                ["type"] = "Ontology",
                ["displayName"] = displayName
            }
        };
        parts.Add(CreatePart(".platform", platform.ToJsonString()));

        // definition.json (empty)
        parts.Add(CreatePart("definition.json", "{}"));

        // EntityTypes
        foreach (var entity in def.EntityTypes)
        {
            var etJson = new JsonObject
            {
                ["$schema"] = "https://developer.microsoft.com/json-schemas/fabric/item/ontology/entityType/1.0.0/schema.json",
                ["id"] = entity.Id,
                ["namespace"] = "usertypes",
                ["baseEntityTypeId"] = null,
                ["name"] = entity.Name,
                ["entityIdParts"] = new JsonArray(JsonValue.Create(entity.KeyPropertyId)),
                ["displayNamePropertyId"] = entity.DisplayNamePropertyId,
                ["namespaceType"] = "Custom",
                ["visibility"] = "Visible",
                ["properties"] = new JsonArray(
                    entity.Properties.Select(p => new JsonObject
                    {
                        ["id"] = p.Id,
                        ["name"] = p.Name,
                        ["redefines"] = null,
                        ["baseTypeNamespaceType"] = null,
                        ["valueType"] = p.ValueType
                    } as JsonNode).ToArray()),
                ["timeseriesProperties"] = new JsonArray(),
                ["untypedProperties"] = new JsonArray()
            };
            parts.Add(CreatePart($"EntityTypes/{entity.Id}/definition.json", etJson.ToJsonString()));

            // DataBinding
            if (entity.Binding != null)
            {
                var bindJson = new JsonObject
                {
                    ["$schema"] = "https://developer.microsoft.com/json-schemas/fabric/item/ontology/dataBinding/1.0.0/schema.json",
                    ["id"] = entity.Binding.Id,
                    ["dataBindingConfiguration"] = new JsonObject
                    {
                        ["dataBindingType"] = "NonTimeSeries",
                        ["propertyBindings"] = new JsonArray(
                            entity.Binding.PropertyBindings.Select(pb => new JsonObject
                            {
                                ["sourceColumnName"] = pb.SourceColumnName,
                                ["targetPropertyId"] = pb.TargetPropertyId
                            } as JsonNode).ToArray()),
                        ["sourceTableProperties"] = new JsonObject
                        {
                            ["sourceType"] = "LakehouseTable",
                            ["workspaceId"] = entity.Binding.WorkspaceId,
                            ["itemId"] = entity.Binding.LakehouseId,
                            ["sourceTableName"] = entity.Binding.SourceTableName,
                            ["sourceSchema"] = (JsonNode?)null
                        }
                    }
                };
                parts.Add(CreatePart(
                    $"EntityTypes/{entity.Id}/DataBindings/{entity.Binding.Id}.json",
                    bindJson.ToJsonString()));
            }
        }

        // RelationshipTypes
        foreach (var rel in def.Relationships)
        {
            var relJson = new JsonObject
            {
                ["namespace"] = "usertypes",
                ["id"] = rel.Id,
                ["name"] = rel.Name,
                ["namespaceType"] = "Custom",
                ["source"] = new JsonObject { ["entityTypeId"] = rel.SourceEntityTypeId },
                ["target"] = new JsonObject { ["entityTypeId"] = rel.TargetEntityTypeId }
            };
            parts.Add(CreatePart(
                $"RelationshipTypes/{rel.Id}/definition.json",
                relJson.ToJsonString()));
        }

        return parts;
    }

    private static JsonObject CreatePart(string path, string jsonContent)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent));
        return new JsonObject
        {
            ["path"] = path,
            ["payload"] = payload,
            ["payloadType"] = "InlineBase64"
        };
    }

    private async Task<string?> ExecuteFabricLroAsync(string azArgs)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "az",
            Arguments = azArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("az CLI の起動に失敗しました。");

        // 60秒タイムアウト付きで待機
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException(
                "Fabric API コールがタイムアウトしました (60秒)。Ontology 機能がこの SKU で利用できない可能性があります。");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        if (process.ExitCode != 0)
        {
            var combined = stderr + "\n" + stdout;
            throw new InvalidOperationException(
                $"Fabric API エラー (exit {process.ExitCode}): {combined[..Math.Min(800, combined.Length)]}");
        }
        // x-ms-operation-id を抽出 (202 LRO の場合)
        var opIdMatch = System.Text.RegularExpressions.Regex.Match(
            stderr, @"x-ms-operation-id['""]?\s*[:=]\s*['""]?([a-f0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (opIdMatch.Success)
            return opIdMatch.Groups[1].Value;

        // Location ヘッダーから operation-id を抽出
        var locMatch = System.Text.RegularExpressions.Regex.Match(
            stderr, @"operations/([a-f0-9\-]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (locMatch.Success)
            return locMatch.Groups[1].Value;

        // 同期完了 (201) — LRO不要
        return null;
    }

    private async Task PollLroAsync(string operationId, CancellationToken ct)
    {
        Console.WriteLine($"    LRO ポーリング中 (operation: {operationId[..8]}...)");

        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            var result = await _az.RunAsync(
                $"rest --method get --url \"{FabricResource}/v1/operations/{operationId}\" --resource \"{FabricResource}\"",
                silent: true);

            var doc = JsonDocument.Parse(result);
            var status = doc.RootElement.GetProperty("status").GetString();

            switch (status)
            {
                case "Succeeded":
                    return;
                case "Failed":
                case "Cancelled":
                    var error = doc.RootElement.TryGetProperty("error", out var errProp)
                        ? errProp.ToString()
                        : "不明なエラー";
                    throw new InvalidOperationException($"Fabric LRO 失敗 ({status}): {error}");
            }
        }

        throw new TimeoutException("Fabric LRO がタイムアウトしました (150秒)。");
    }

    private static string MapColumnTypeToValueType(string columnType)
    {
        var normalized = columnType.ToLowerInvariant().Trim();
        return normalized switch
        {
            "int" or "bigint" or "smallint" or "tinyint" => "BigInt",
            "float" or "double" or "decimal" or "numeric" or "real" or "money" => "Double",
            "bit" or "boolean" => "Boolean",
            "datetime" or "datetime2" or "date" or "timestamp" or "datetimeoffset" => "DateTime",
            _ => "String" // nvarchar, varchar, uniqueidentifier, etc.
        };
    }

    private static string ToPascalCase(string tableName)
    {
        // snake_case → PascalCase, 末尾の s は除去してエンティティ名にする
        var parts = tableName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (sb.Length == 0 && part.Length > 1 && part.EndsWith('s') && !part.EndsWith("ss"))
            {
                // 先頭パートの複数形は維持（テーブル名としては自然）
            }
            sb.Append(char.ToUpperInvariant(part[0]));
            sb.Append(part[1..]);
        }

        var result = sb.ToString();
        // 末尾 s を除去 (customers → Customer)
        if (result.Length > 2 && result.EndsWith('s') && !result.EndsWith("ss"))
            result = result[..^1];

        return result;
    }
}

// --- 内部モデル ---

internal class OntologyDefinition
{
    public List<OntologyEntityType> EntityTypes { get; set; } = [];
    public List<OntologyRelationship> Relationships { get; set; } = [];
}

internal class OntologyEntityType
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string KeyPropertyId { get; set; } = string.Empty;
    public string DisplayNamePropertyId { get; set; } = string.Empty;
    public List<OntologyProperty> Properties { get; set; } = [];
    public OntologyDataBinding? Binding { get; set; }
}

internal class OntologyProperty
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ValueType { get; set; } = "String";
}

internal class OntologyDataBinding
{
    public string Id { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string LakehouseId { get; set; } = string.Empty;
    public string SourceTableName { get; set; } = string.Empty;
    public List<PropertyBinding> PropertyBindings { get; set; } = [];
}

internal class PropertyBinding
{
    public string SourceColumnName { get; set; } = string.Empty;
    public string TargetPropertyId { get; set; } = string.Empty;
}

internal class OntologyRelationship
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceEntityTypeId { get; set; } = string.Empty;
    public string TargetEntityTypeId { get; set; } = string.Empty;
}
