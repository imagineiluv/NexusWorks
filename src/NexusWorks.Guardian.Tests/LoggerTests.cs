using FluentAssertions;
using NexusWorks.Guardian.Infrastructure;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Infrastructure")]
public class LoggerTests
{
    // ── NullGuardianLogger ──

    [Fact]
    public void NullLogger_should_be_singleton()
    {
        var a = NullGuardianLogger.Instance;
        var b = NullGuardianLogger.Instance;

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void NullLogger_should_not_throw_on_any_operation()
    {
        var logger = NullGuardianLogger.Instance;

        var act = () =>
        {
            logger.Info("test");
            logger.Warn("test");
            logger.Error("test", new Exception("err"));
            logger.Error("test");
            logger.StageStart("stage");
            logger.StageEnd("stage", 10, 100.5);
            logger.ItemError("path/file.xml", "R001", new InvalidOperationException("boom"));
        };

        act.Should().NotThrow();
    }

    // ── BufferedGuardianLogger ──

    [Fact]
    public void BufferedLogger_should_capture_info_messages()
    {
        var logger = new BufferedGuardianLogger();

        logger.Info("Hello");
        logger.Info("World");

        logger.Entries.Should().HaveCount(2);
        logger.Entries[0].Level.Should().Be(BufferedGuardianLogger.LogLevel.Info);
        logger.Entries[0].Message.Should().Be("Hello");
        logger.Entries[1].Message.Should().Be("World");
    }

    [Fact]
    public void BufferedLogger_should_capture_warn_messages()
    {
        var logger = new BufferedGuardianLogger();

        logger.Warn("caution");

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(BufferedGuardianLogger.LogLevel.Warn);
    }

    [Fact]
    public void BufferedLogger_should_capture_error_with_exception()
    {
        var logger = new BufferedGuardianLogger();
        var ex = new InvalidOperationException("test error");

        logger.Error("something failed", ex);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(BufferedGuardianLogger.LogLevel.Error);
        logger.Entries[0].Exception.Should().BeSameAs(ex);
        logger.Errors.Should().ContainSingle();
    }

    [Fact]
    public void BufferedLogger_should_capture_error_without_exception()
    {
        var logger = new BufferedGuardianLogger();

        logger.Error("plain error");

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Exception.Should().BeNull();
        logger.Errors.Should().ContainSingle();
    }

    [Fact]
    public void BufferedLogger_should_capture_stage_start_and_end()
    {
        var logger = new BufferedGuardianLogger();

        logger.StageStart("Baseline Load");
        logger.StageEnd("Baseline Load", 42, 123.45);

        logger.Entries.Should().HaveCount(2);
        logger.Entries[0].Message.Should().Contain("Stage started: Baseline Load");
        logger.Entries[1].Message.Should().Contain("Stage completed: Baseline Load");
        logger.Entries[1].Message.Should().Contain("42 items");
    }

    [Fact]
    public void BufferedLogger_should_capture_item_error_with_full_context()
    {
        var logger = new BufferedGuardianLogger();
        var ex = new IOException("file locked");

        logger.ItemError("conf/app.xml", "R001", ex);

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries[0];
        entry.Level.Should().Be(BufferedGuardianLogger.LogLevel.ItemError);
        entry.RelativePath.Should().Be("conf/app.xml");
        entry.RuleId.Should().Be("R001");
        entry.Exception.Should().BeSameAs(ex);
        entry.Message.Should().Contain("conf/app.xml");
        entry.Message.Should().Contain("R001");
    }

    [Fact]
    public void BufferedLogger_Errors_should_include_both_Error_and_ItemError()
    {
        var logger = new BufferedGuardianLogger();

        logger.Info("ignored");
        logger.Warn("also ignored");
        logger.Error("error one");
        logger.ItemError("path", "R001", new Exception("item err"));

        logger.Errors.Should().HaveCount(2);
        logger.Errors.Should().OnlyContain(e =>
            e.Level == BufferedGuardianLogger.LogLevel.Error || e.Level == BufferedGuardianLogger.LogLevel.ItemError);
    }

    [Fact]
    public async Task BufferedLogger_should_be_thread_safe()
    {
        var logger = new BufferedGuardianLogger();
        const int threadCount = 10;
        const int messagesPerThread = 100;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(() =>
            {
                for (var i = 0; i < messagesPerThread; i++)
                {
                    logger.Info($"Thread {t} message {i}");
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        logger.Entries.Should().HaveCount(threadCount * messagesPerThread);
    }

    [Fact]
    public async Task BufferedLogger_should_be_thread_safe_with_mixed_operations()
    {
        var logger = new BufferedGuardianLogger();
        const int threadCount = 8;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(t => Task.Run(() =>
            {
                logger.Info($"info-{t}");
                logger.Warn($"warn-{t}");
                logger.Error($"error-{t}");
                logger.StageStart($"stage-{t}");
                logger.StageEnd($"stage-{t}", t, t * 10.0);
                logger.ItemError($"path-{t}", $"R{t:D3}", new Exception($"ex-{t}"));
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        // 6 messages per thread
        logger.Entries.Should().HaveCount(threadCount * 6);
    }
}
