using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Vertical_Slice_Clean_Architecture.Infrastructure.Correlation;

namespace Vertical_Slice_Clean_Architecture.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="CorrelationIdMiddleware"/> and <see cref="CorrelationIdAccessor"/>.
/// The response header itself is covered by the host tests, because it is written when the
/// response starts.
/// </summary>
[TestFixture]
public class CorrelationIdMiddlewareTests
{
    /// <summary>
    /// Short values made of ID characters are accepted.
    /// </summary>
    /// <param name="value">The incoming header value.</param>
    [TestCase("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [TestCase("order-123:abc_DEF.7")]
    [TestCase("a")]
    public void IsValid_Should_AcceptPlainIds(string value)
    {
        Assert.That(CorrelationIdMiddleware.IsValid(value), Is.True);
    }

    /// <summary>
    /// Empty, oversized, multi-valued and markup-like values are rejected, because the value is
    /// echoed in a response header and written to logs.
    /// </summary>
    /// <param name="value">The incoming header value.</param>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("has space")]
    [TestCase("a,b")]
    [TestCase("<script>")]
    [TestCase("line\r\nbreak")]
    public void IsValid_Should_RejectUnsafeValues(string? value)
    {
        Assert.That(CorrelationIdMiddleware.IsValid(value), Is.False);
    }

    /// <summary>
    /// The length limit is inclusive.
    /// </summary>
    [Test]
    public void IsValid_Should_EnforceTheMaximumLength()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CorrelationIdMiddleware.IsValid(new string('a', CorrelationIdMiddleware.MaxLength)), Is.True);
            Assert.That(CorrelationIdMiddleware.IsValid(new string('a', CorrelationIdMiddleware.MaxLength + 1)), Is.False);
        }
    }

    /// <summary>
    /// A valid incoming ID is what the rest of the pipeline sees through the accessor.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvokeAsync_Should_ReuseAValidIncomingId()
    {
        var accessor = new CorrelationIdAccessor();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "order-123";
        string? seenDownstream = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            seenDownstream = accessor.CorrelationId;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor, NullLogger<CorrelationIdMiddleware>.Instance);

        Assert.That(seenDownstream, Is.EqualTo("order-123"));
    }

    /// <summary>
    /// Without a usable incoming ID, a new GUID is generated for the request.
    /// </summary>
    /// <param name="incoming">The incoming header value, or <see langword="null"/> for none.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestCase(null)]
    [TestCase("<script>")]
    public async Task InvokeAsync_Should_GenerateAnIdWhenNoneIsUsable(string? incoming)
    {
        var accessor = new CorrelationIdAccessor();
        var context = new DefaultHttpContext();
        if (incoming is not null)
        {
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        }

        string? seenDownstream = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            seenDownstream = accessor.CorrelationId;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, accessor, NullLogger<CorrelationIdMiddleware>.Instance);

        Assert.That(Guid.TryParse(seenDownstream, out _), Is.True, $"'{seenDownstream}' is not a GUID.");
    }

    /// <summary>
    /// Concurrent flows keep their own IDs, so one request never sees another's.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Accessor_Should_IsolateConcurrentFlows()
    {
        var accessor = new CorrelationIdAccessor();

        var observed = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            accessor.Set($"flow-{i}");
            await Task.Delay(10);
            return accessor.CorrelationId == $"flow-{i}";
        })));

        Assert.That(observed, Is.All.True);
    }
}
