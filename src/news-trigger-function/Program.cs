using Azure.Core;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewsPortalTriggerFunction;

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient();
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton<QueueClient>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var account = configuration["Storage__Account"] ?? configuration["Storage:Account"];
            var queueName = configuration["Storage__QueueName"] ?? configuration["Storage:QueueName"] ?? "news-analysis-jobs";
            if (string.IsNullOrWhiteSpace(account))
            {
                throw new InvalidOperationException("Storage account is not configured. Set Storage__Account.");
            }

            return new QueueClient(
                new Uri($"https://{account}.queue.core.windows.net/{queueName}"),
                sp.GetRequiredService<TokenCredential>(),
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        });
        services.AddSingleton<NewsPortalCrawler>();
    })
    .Build();

await host.RunAsync();
