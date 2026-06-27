namespace DemoDataGenerator.Data.Seeding;

public interface IScenarioSeeder
{
    ScenarioKind Scenario { get; }
    Task SeedAsync(DemoDataRequest request, CancellationToken cancellationToken = default);
}
