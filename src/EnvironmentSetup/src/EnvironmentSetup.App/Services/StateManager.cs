using System.Text.Json;
using EnvironmentSetup.App.Models;

namespace EnvironmentSetup.App.Services;

/// <summary>
/// setup-state.json の読み書きを管理する
/// </summary>
public class StateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public StateManager(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<SetupState> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new SetupState();

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<SetupState>(json, JsonOptions) ?? new SetupState();
    }

    public async Task SaveAsync(SetupState state)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task MarkStepCompletedAsync(SetupState state, int stepNumber)
    {
        if (!state.CompletedSteps.Contains(stepNumber))
            state.CompletedSteps.Add(stepNumber);

        state.CurrentStep = stepNumber + 1;
        await SaveAsync(state);
    }

    public bool IsStepCompleted(SetupState state, int stepNumber)
        => state.CompletedSteps.Contains(stepNumber);
}
