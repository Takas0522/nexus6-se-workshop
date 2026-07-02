using System.Diagnostics;
using System.Text;

namespace EnvironmentSetup.App.Services;

/// <summary>
/// Azure CLI (az) コマンドのラッパー
/// </summary>
public class AzureCliWrapper
{
    /// <summary>
    /// az CLI コマンドを実行し、標準出力を返す
    /// </summary>
    public async Task<string> RunAsync(string arguments, bool silent = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start az CLI process");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
                if (!silent)
                    Console.WriteLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderr.AppendLine(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"az CLI failed (exit {process.ExitCode}): {stderr}");

        return stdout.ToString().Trim();
    }

    /// <summary>
    /// 既存のログインセッションが有効か確認する
    /// </summary>
    public async Task<bool> CheckLoginAsync()
    {
        try
        {
            await RunAsync("account show --output json", silent: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// デバイスコードフローでログインする
    /// </summary>
    public async Task LoginAsync()
    {
        await RunAsync("login --use-device-code");
    }

    /// <summary>
    /// Bicep テンプレートの what-if を実行する
    /// </summary>
    public async Task<string> WhatIfAsync(string resourceGroup, string templateFile, string? paramFile = null)
    {
        var args = $"deployment group what-if --resource-group {resourceGroup} --template-file {templateFile}";
        if (paramFile != null)
            args += $" --parameters {paramFile}";
        return await RunAsync(args);
    }

    /// <summary>
    /// Bicep テンプレートをデプロイする
    /// </summary>
    public async Task<string> DeployAsync(string resourceGroup, string templateFile, string? paramFile = null)
    {
        var args = $"deployment group create --resource-group {resourceGroup} --template-file {templateFile} --output json";
        if (paramFile != null)
            args += $" --parameters {paramFile}";
        return await RunAsync(args, silent: true);
    }

    /// <summary>
    /// リソースグループを作成する
    /// </summary>
    public async Task EnsureResourceGroupAsync(string name, string location)
    {
        await RunAsync($"group create --name {name} --location {location} --output none", silent: true);
    }
}
