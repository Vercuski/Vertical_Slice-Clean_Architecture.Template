#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

namespace Vertical_Slice_Clean_Architecture.Tests.UnitTests;

/// <summary>
/// Guards the CQRS split of the EF Core contexts: the query side can only query, and the command
/// side can do everything except query.
/// </summary>
[TestFixture]
public class PersistenceContextTests
{
    /// <summary>
    /// The concrete contexts are internal, so slices can reach them only through
    /// <see cref="IQueryDbContext"/> and <see cref="ICommandDbContext"/>.
    /// </summary>
    [Test]
    public void Contexts_Should_NotBePublic()
    {
        var publicContexts = typeof(IQueryDbContext).Assembly.GetTypes()
            .Where(type => typeof(DbContext).IsAssignableFrom(type) && type.IsPublic)
            .Select(type => type.FullName);

        Assert.That(publicContexts, Is.Empty);
    }

    /// <summary>
    /// <see cref="IQueryDbContext"/> has no properties and every method returns an <see cref="IQueryable{T}"/>,
    /// so nothing on it can change data.
    /// </summary>
    [Test]
    public void QueryContext_Should_ExposeOnlyQueryables()
    {
        var type = typeof(IQueryDbContext);
        var nonQueryables = type.GetMethods()
            .Where(method => !method.ReturnType.IsGenericType
                || method.ReturnType.GetGenericTypeDefinition() != typeof(IQueryable<>))
            .Select(method => method.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(type.GetProperties(), Is.Empty);
            Assert.That(nonQueryables, Is.Empty);
        }
    }

    /// <summary>
    /// Nothing on <see cref="ICommandDbContext"/> or <see cref="IWriteSet{TEntity}"/> returns or is
    /// enumerable data, so nothing on the command side can query.
    /// </summary>
    [Test]
    public void CommandContext_Should_ExposeNothingQueryable()
    {
        Type[] types = [typeof(ICommandDbContext), typeof(IWriteSet<>)];
        var queryable = types
            .SelectMany(type => type.GetProperties().Select(property => property.PropertyType)
                .Concat(type.GetMethods().Select(method => method.ReturnType))
                .Concat(type.GetInterfaces()))
            .Where(type => type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            .Select(type => type.Name);

        Assert.That(queryable, Is.Empty);
    }

    /// <summary>
    /// Saving through the query context throws, synchronously and asynchronously, even for code inside
    /// Infrastructure that can reach the concrete type.
    /// </summary>
    [Test]
    public void QueryContext_Should_ThrowOnSave()
    {
        using var context = new QueryDbContext(new DbContextOptionsBuilder<QueryDbContext>().Options);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => context.SaveChanges(), Throws.InvalidOperationException.With.Message.EqualTo(QueryDbContext.ReadOnlyMessage));
            Assert.That(() => context.SaveChangesAsync(), Throws.InvalidOperationException.With.Message.EqualTo(QueryDbContext.ReadOnlyMessage));
        }
    }
}
#endif
