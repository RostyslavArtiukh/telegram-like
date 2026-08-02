using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using TelegramLike.Shared.Infrastructure;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Chats.Tests.Shared;

/// <summary>
/// The shared index mechanism. Lives in this project because it already sees
/// <c>TelegramLike.Shared.Infrastructure</c>'s internals; it needs no Mongo of its own —
/// what's under test is which declarations run, not what they build.
/// </summary>
public class MongoIndexesTests
{
    private sealed class FakeIndexes(string collection) : IMongoIndexes
    {
        public string Collection => collection;
        public IMongoDatabase? AppliedTo { get; private set; }

        public Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
        {
            AppliedTo = database;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Sink(Entries);
        public void Dispose() { }

        private sealed class Sink(List<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private static ServiceProvider Build(
        CapturingLoggerProvider logs, params IMongoIndexes[] declarations)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddScoped(_ => Substitute.For<IMongoDatabase>());
        foreach (var declaration in declarations) services.AddSingleton(declaration);
        services.AddSingleton<IHostedService, MongoIndexInitializer>();
        return services.BuildServiceProvider();
    }

    private static IHostedService Initializer(ServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<MongoIndexInitializer>().Single();

    [Fact]
    public async Task AppliesEveryDeclaredIndexSet()
    {
        var members = new FakeIndexes("chat_members");
        var outbox = new FakeIndexes("outgoing_events");
        var logs = new CapturingLoggerProvider();
        await using var provider = Build(logs, members, outbox);

        await Initializer(provider).StartAsync(CancellationToken.None);

        members.AppliedTo.Should().NotBeNull();
        outbox.AppliedTo.Should().NotBeNull("one missed declaration is a collection left unindexed");
        logs.Entries.Should().Contain(e => e.Message.Contains("chat_members"))
            .And.Contain(e => e.Message.Contains("outgoing_events"));
    }

    [Fact]
    public async Task WarnsWhenAServiceDeclaresNoIndexesAtAll()
    {
        // The signal that was missing: a service could simply never write an initializer —
        // Presence still hasn't — and nothing anywhere said so.
        var logs = new CapturingLoggerProvider();
        await using var provider = Build(logs);

        await Initializer(provider).StartAsync(CancellationToken.None);

        logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning)
            .Which.Message.Should().Contain("No Mongo indexes are declared");
    }

    [Fact]
    public void AddMongoDbRegistersTheInitializerEvenWithoutDeclarations()
    {
        // Deliberately hung off the database rather than off AddMongoIndexes<T>: a service
        // that declares nothing registers nothing, and would never be heard from.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDB:DatabaseName"] = "test"
            })
            .Build();

        var services = new ServiceCollection().AddMongoDb(configuration);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(MongoIndexInitializer));
    }

    [Fact]
    public void DeclaringTheSameIndexSetTwiceRegistersItOnce()
    {
        var services = new ServiceCollection()
            .AddMongoIndexes<OutgoingEventsIndexes>()
            .AddMongoIndexes<OutgoingEventsIndexes>();

        services.Count(d => d.ServiceType == typeof(IMongoIndexes)).Should().Be(1);
    }
}
