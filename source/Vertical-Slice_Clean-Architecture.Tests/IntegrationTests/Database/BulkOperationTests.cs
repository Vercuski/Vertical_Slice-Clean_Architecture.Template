#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vertical_Slice_Clean_Architecture.Domain.Entities;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

namespace Vertical_Slice_Clean_Architecture.Tests.IntegrationTests.Database;

/// <summary>
/// Runs the bulk operations on <see cref="ICommandDbContext"/> against a real database of this
/// solution's engine, and reads the results back through <see cref="IQueryDbContext"/>.
/// </summary>
/// <remarks>
/// Every test uses its own email addresses, except <see cref="Truncate_Should_EmptyTheTable"/>,
/// which empties the whole <c>Samples</c> table and so checks only that it is empty afterwards.
/// </remarks>
[TestFixture]
public class BulkOperationTests
{
    /// <summary>
    /// Bulk-inserted entities can all be read back.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BulkInsert_Should_InsertEveryEntity()
    {
        var samples = NewSamples(3);

        await CommandAsync(db => db.BulkInsertAsync(samples));

        Assert.That(await ReadAsync(samples), Has.Count.EqualTo(3));
    }

    /// <summary>
    /// Changes made to existing entities are written by a bulk update.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BulkUpdate_Should_WriteChanges()
    {
        var samples = NewSamples(2);
        await CommandAsync(db => db.BulkInsertAsync(samples));

        samples.ForEach(sample => sample.Deactivate());
        await CommandAsync(db => db.BulkUpdateAsync(samples));

        Assert.That((await ReadAsync(samples)).Select(sample => sample.IsActive), Is.All.False);
    }

    /// <summary>
    /// An upsert updates the entities that exist and inserts the ones that don't.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BulkInsertOrUpdate_Should_InsertNewAndUpdateExisting()
    {
        var existing = NewSamples(1)[0];
        await CommandAsync(db => db.BulkInsertAsync([existing]));

        existing.Deactivate();
        var added = NewSamples(1)[0];
        await CommandAsync(db => db.BulkInsertOrUpdateAsync([existing, added]));

        var stored = await ReadAsync([existing, added]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored, Has.Count.EqualTo(2));
            Assert.That(stored.Single(sample => sample.Id == existing.Id).IsActive, Is.False);
            Assert.That(stored.Single(sample => sample.Id == added.Id).IsActive, Is.True);
        }
    }

    /// <summary>
    /// Bulk-deleted entities are gone; others are untouched.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BulkDelete_Should_RemoveOnlyTheGivenEntities()
    {
        var samples = NewSamples(3);
        await CommandAsync(db => db.BulkInsertAsync(samples));

        await CommandAsync(db => db.BulkDeleteAsync(samples.Take(2)));

        Assert.That((await ReadAsync(samples)).Select(sample => sample.Id), Is.EqualTo(new[] { samples[2].Id }));
    }

    /// <summary>
    /// Changes staged through <see cref="ICommandDbContext.Set{TEntity}"/> are written by a bulk save.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BulkSaveChanges_Should_WriteStagedChanges()
    {
        var samples = NewSamples(2);

        await CommandAsync(db =>
        {
            db.Set<SampleEntity>().AddRange(samples);
            return db.BulkSaveChangesAsync();
        });

        Assert.That(await ReadAsync(samples), Has.Count.EqualTo(2));
    }
#if (HasSqlServer)

    /// <summary>
    /// A synchronize leaves the table holding exactly the given entities.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BulkInsertOrUpdateOrDelete_Should_MakeTheTableMatch()
    {
        var removed = NewSamples(1)[0];
        var kept = NewSamples(1)[0];
        await CommandAsync(db => db.BulkInsertAsync([removed, kept]));

        kept.Deactivate();
        var added = NewSamples(1)[0];
        await CommandAsync(db => db.BulkInsertOrUpdateOrDeleteAsync([kept, added]));

        var all = await ReadAllAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.Select(sample => sample.Id), Is.EquivalentTo(new[] { kept.Id, added.Id }));
            Assert.That(all.Single(sample => sample.Id == kept.Id).IsActive, Is.False);
        }
    }
#endif

    /// <summary>
    /// Truncating the entity's table removes every row.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Truncate_Should_EmptyTheTable()
    {
        await CommandAsync(db => db.BulkInsertAsync(NewSamples(2)));

        await CommandAsync(db => db.TruncateAsync<SampleEntity>());

        Assert.That(await ReadAllAsync(), Is.Empty);
    }

    /// <summary>
    /// Runs <paramref name="action"/> against a command context in its own scope.
    /// </summary>
    /// <param name="action">The work to do.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task CommandAsync(Func<ICommandDbContext, Task> action)
    {
        using var scope = DatabaseSetUp.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ICommandDbContext>());
    }

    /// <summary>
    /// Reads the stored rows for <paramref name="samples"/>, in a fresh scope.
    /// </summary>
    /// <param name="samples">The samples to look up, by identifier.</param>
    /// <returns>The rows found, ordered by email.</returns>
    private static async Task<List<SampleEntity>> ReadAsync(IEnumerable<SampleEntity> samples)
    {
        var ids = samples.Select(sample => sample.Id).ToList();
        using var scope = DatabaseSetUp.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryDbContext>().Set<SampleEntity>()
            .Where(sample => ids.Contains(sample.Id))
            .OrderBy(sample => sample.Email)
            .ToListAsync();
    }

    /// <summary>
    /// Reads every row in the <c>Samples</c> table, in a fresh scope.
    /// </summary>
    /// <returns>All rows.</returns>
    private static async Task<List<SampleEntity>> ReadAllAsync()
    {
        using var scope = DatabaseSetUp.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryDbContext>().Set<SampleEntity>().ToListAsync();
    }

    /// <summary>
    /// Creates <paramref name="count"/> new samples with email addresses no other test has used, ordered by email.
    /// </summary>
    /// <param name="count">How many to create.</param>
    /// <returns>The samples.</returns>
    private static List<SampleEntity> NewSamples(int count)
    {
        var batch = Guid.NewGuid().ToString("N");
        return Enumerable.Range(0, count)
            .Select(index => SampleEntity.Create($"Bulk {index}", $"{batch}-{index}@example.com"))
            .ToList();
    }
}
#endif
