using FluentAssertions;
using TelegramLike.Web.Services;

namespace TelegramLike.Web.Tests.Services;

/// <summary>
/// The pubsub registry is a singleton on a host whose memory already scales with open tabs,
/// so anything it forgets to release is held for the life of the process. It used to keep a
/// per-topic entry forever once created — growing with every chat ever opened and every user
/// ever rendered, never with anything that shrinks.
/// </summary>
public class CircuitTopicsTests
{
    [Fact]
    public async Task Publish_ReachesEverySubscriberOfThatTopicAndNoOther()
    {
        var topics = new CircuitTopics<Func<int, Task>>();
        var chat = Guid.NewGuid();
        var otherChat = Guid.NewGuid();
        var seen = new List<int>();
        var seenElsewhere = new List<int>();

        using var _ = topics.Subscribe(chat, v => { seen.Add(v); return Task.CompletedTask; });
        using var __ = topics.Subscribe(chat, v => { seen.Add(v * 10); return Task.CompletedTask; });
        using var ___ = topics.Subscribe(otherChat, v => { seenElsewhere.Add(v); return Task.CompletedTask; });

        await topics.PublishAsync(chat, cb => cb(1));

        seen.Should().BeEquivalentTo([1, 10]);
        seenElsewhere.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_KeepsGoingWhenOneSubscriberThrows()
    {
        // A circuit that faulted mid-callback must not stop the others from redrawing.
        var topics = new CircuitTopics<Func<Task>>();
        var topic = Guid.NewGuid();
        var reached = false;

        using var _ = topics.Subscribe(topic, () => throw new InvalidOperationException("circuit is gone"));
        using var __ = topics.Subscribe(topic, () => { reached = true; return Task.CompletedTask; });

        await topics.PublishAsync(topic, cb => cb());

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task DisposingASubscription_StopsItsCallback_ButLeavesTheOthers()
    {
        var topics = new CircuitTopics<Func<Task>>();
        var topic = Guid.NewGuid();
        var goneCalls = 0;
        var stayingCalls = 0;

        var gone = topics.Subscribe(topic, () => { goneCalls++; return Task.CompletedTask; });
        using var staying = topics.Subscribe(topic, () => { stayingCalls++; return Task.CompletedTask; });

        gone.Dispose();
        await topics.PublishAsync(topic, cb => cb());

        goneCalls.Should().Be(0);
        stayingCalls.Should().Be(1);
    }

    [Fact]
    public void ATopicIsForgottenOnceItsLastSubscriberLeaves()
    {
        // The leak: every chat opened and every member rendered left an entry behind, so a
        // long-lived replica's registry only ever grew.
        var topics = new CircuitTopics<Func<Task>>();

        foreach (var _ in Enumerable.Range(0, 100))
        {
            var subscription = topics.Subscribe(Guid.NewGuid(), () => Task.CompletedTask);
            subscription.Dispose();
        }

        topics.TopicCount.Should().Be(0);
    }

    [Fact]
    public void ATopicSurvivesWhileAnySubscriberRemains()
    {
        var topics = new CircuitTopics<Func<Task>>();
        var topic = Guid.NewGuid();

        var first = topics.Subscribe(topic, () => Task.CompletedTask);
        using var second = topics.Subscribe(topic, () => Task.CompletedTask);
        first.Dispose();

        topics.TopicCount.Should().Be(1);
    }

    [Fact]
    public async Task ASubscribeRacingTheLastUnsubscribe_StillReceivesPublishes()
    {
        // Two tabs on the same chat, one closing as the other opens. Dropping the topic must
        // never orphan the arriving subscriber: it would look like real-time silently dying
        // for that chat, with nothing logged.
        var topics = new CircuitTopics<Func<Task>>();
        var topic = Guid.NewGuid();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var leaving = topics.Subscribe(topic, () => Task.CompletedTask);
            var arrivedCalls = 0;

            var closing = Task.Run(leaving.Dispose);
            var opening = Task.Run(() => topics.Subscribe(topic, () => { arrivedCalls++; return Task.CompletedTask; }));

            await Task.WhenAll(closing, opening);
            using var arrived = await opening;

            await topics.PublishAsync(topic, cb => cb());
            arrivedCalls.Should().Be(1, "the subscriber that arrived during the removal must still be reachable");
        }
    }
}
