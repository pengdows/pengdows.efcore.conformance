using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

/// <summary>
///     Black-box execution of live model-driver property cases. Each case reads a property through EF's public model metadata of a
///     model built from the provider. A case is executed when the read returns; an exception is recorded as an error. No expected
///     value is asserted here: the outcomes are the input to the cross-provider comparison.
/// </summary>
public sealed class BlackboxModelPropertyTests
{
    [Fact]
    public void Executes_live_model_property_cases_and_records_outcomes()
    {
        var casesPath = Environment.GetEnvironmentVariable("BLACKBOX_CASES");
        var resultsPath = Environment.GetEnvironmentVariable("BLACKBOX_RESULTS");
        if (casesPath is null || resultsPath is null)
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(casesPath));
        var cases = document.RootElement.GetProperty("Items").EnumerateArray()
            .Where(c => c.GetProperty("Exclusion").ValueKind == JsonValueKind.Null
                        && c.GetProperty("Driver").GetString() == "model"
                        && c.GetProperty("MemberKind").GetString() == "Property")
            .ToList();

        var options = new DbContextOptionsBuilder<BlackboxModelContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        using var context = new BlackboxModelContext(options.Options);
        var model = context.Model;
        var receivers = BuildReceivers(model);

        var results = new List<object>();
        foreach (var c in cases)
        {
            var declaring = c.GetProperty("DeclaringType").GetString()!;
            var member = c.GetProperty("Member").GetString()!;
            var id = c.GetProperty("Id").GetString()!;
            var receiver = receivers.TryGetValue(declaring.Split('.').Last().Split('`')[0], out var found) ? found : null;
            if (receiver is null)
            {
                results.Add(new { Id = id, Outcome = "NotRun", Reason = "no receiver built for this declaring type" });
                continue;
            }

            try
            {
                var property = receiver.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                if (property is null)
                {
                    results.Add(new { Id = id, Outcome = "NotRun", Reason = $"receiver has no property {member}" });
                    continue;
                }

                _ = property.GetValue(receiver);
                results.Add(new { Id = id, Outcome = "Executed", Reason = (string?)null });
            }
            catch (TargetInvocationException exception)
            {
                results.Add(new { Id = id, Outcome = "Error", Reason = exception.InnerException?.GetType().FullName });
            }
        }

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(new
        {
            Cases = cases.Count,
            Executed = results.Count(r => r.GetType().GetProperty("Outcome")!.GetValue(r) as string == "Executed"),
            Items = results,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Walks the model's public object graph (parameterless getters that return metadata objects or sequences of them), bounded in depth
    // and size, and keeps the first object of each declaring type it reaches. Each property case then reads through a real object
    // EF's public API produced, not one this test constructed.
    private static Dictionary<string, object> BuildReceivers(IModel model)
    {
        var found = new Dictionary<string, object>(StringComparer.Ordinal);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<(object Node, int Depth)>();
        queue.Enqueue((model, 0));

        // Public extension methods that hand out metadata (the relational model, sequences, functions, and check constraints) are roots too.
        foreach (var root in ExtensionRoots(model))
        {
            queue.Enqueue((root, 1));
        }
        while (queue.Count > 0 && seen.Count < 20000)
        {
            var (node, depth) = queue.Dequeue();
            if (!seen.Add(node))
            {
                continue;
            }

            foreach (var type in node.GetType().GetInterfaces().Append(node.GetType()))
            {
                found.TryAdd(type.Name.Split('`')[0], node);
            }

            if (depth >= 5)
            {
                continue;
            }

            if (depth >= 1)
            {
                foreach (var extended in ExtensionRoots(node))
                {
                    queue.Enqueue((extended, depth + 1));
                }
            }

            var getters = node.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetParameters().Length == 0 && m.ReturnType != typeof(void) && m.Name.StartsWith("Get", StringComparison.Ordinal))
                .Select(m => (Func<object?>)(() => m.Invoke(node, null)))
                .Concat(node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead)
                    .Select(p => (Func<object?>)(() => p.GetValue(node))));
            foreach (var read in getters)
            {
                object? value;
                try
                {
                    value = read();
                }
                catch (Exception)
                {
                    continue;
                }

                switch (value)
                {
                    case null or string:
                        continue;
                    case System.Collections.IEnumerable items and not string:
                        foreach (var item in items.Cast<object?>().Where(i => i is not null).Take(200))
                        {
                            queue.Enqueue((item!, depth + 1));
                        }

                        break;
                    default:
                        if (value.GetType().Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true)
                        {
                            queue.Enqueue((value, depth + 1));
                        }

                        break;
                }
            }
        }

        return found;
    }

    private static IEnumerable<object> ExtensionRoots(object model)
    {
        var extensionTypes = typeof(DbContext).Assembly.GetTypes()
            .Where(t => t.IsAbstract && t.IsSealed && t.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var method in extensionTypes.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                     .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(model) && m.ReturnType != typeof(void)))
        {
            object? value;
            try
            {
                value = method.Invoke(null, [model]);
            }
            catch (Exception)
            {
                continue;
            }

            if (value is not null && value is not string && value is not System.Collections.IEnumerable)
            {
                yield return value;
            }
            else if (value is System.Collections.IEnumerable items and not string)
            {
                foreach (var item in items.Cast<object?>().Where(i => i is not null).Take(200))
                {
                    yield return item!;
                }
            }
        }
    }

    public sealed class BlackboxRow
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
    }

    public sealed class BlackboxParent
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
    }

    public sealed class BlackboxChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public BlackboxParent? Parent { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class BlackboxModelContext(DbContextOptions<BlackboxModelContext> options) : DbContext(options)
    {
        public DbSet<BlackboxRow> Rows => Set<BlackboxRow>();
        public DbSet<BlackboxParent> Parents => Set<BlackboxParent>();
        public DbSet<BlackboxChild> Children => Set<BlackboxChild>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("blackbox_seq").StartsAt(10).IncrementsBy(2);
            modelBuilder.Entity<BlackboxRow>(e =>
            {
                e.Property(r => r.Id).ValueGeneratedNever();
                e.HasIndex(r => r.Label).HasDatabaseName("ix_blackbox_label");
                e.ToTable(t => t.HasCheckConstraint("ck_blackbox_label", "label <> ''"));
            });
            modelBuilder.Entity<BlackboxParent>(e =>
            {
                e.Property(p => p.Id).ValueGeneratedNever();
                e.HasAlternateKey(p => p.Code);
            });
            modelBuilder.Entity<BlackboxChild>(e =>
            {
                e.Property(c => c.Id).ValueGeneratedNever();
                e.HasOne(c => c.Parent).WithMany().HasForeignKey(c => c.ParentId);
                e.HasIndex(c => c.Quantity);
            });
        }
    }
}
