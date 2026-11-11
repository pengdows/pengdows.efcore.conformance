using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

/// <summary>
///     Column and table fluent configuration, checked on the database: ToTable names the table, HasColumnName names the column,
///     IsRequired makes the column NOT NULL, HasIndex/HasDatabaseName creates the named index, and HasComment writes the comment.
///     The model is built here, and each test creates its own tables with the provider's DDL and drops them afterwards.
/// </summary>
[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class FluentColumnConfigurationTests
{
    private const string RowTable = "fluent_row";
    private const string IndexedTable = "fluent_indexed";
    private const string CommentedTable = "fluent_commented";

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-TO-TABLE-SYNC", "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.ToTable")]
    public async Task ToTable_names_the_table_the_rows_are_written_to_synchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var context = CreateContext();
            context.Rows.Add(new FluentRow { Id = 1, Label = "to table", Amount = 2 });
            context.SaveChanges();

            Assert.Equal(1, CountRows(RowTable));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-TO-TABLE-ASYNC", "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.ToTable")]
    public async Task ToTable_names_the_table_the_rows_are_written_to_asynchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var context = CreateContext();
            context.Rows.Add(new FluentRow { Id = 1, Label = "to table", Amount = 2 });
            await context.SaveChangesAsync(ct);

            Assert.Equal(1, CountRows(RowTable));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-HAS-COLUMN-NAME-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.ColumnBuilder.HasColumnName", "Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions.HasColumnName")]
    public async Task HasColumnName_stores_the_value_under_the_configured_column_synchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using (var context = CreateContext())
            {
                context.Rows.Add(new FluentRow { Id = 1, Label = "named column", Amount = 2 });
                context.SaveChanges();
            }

            Assert.Equal("named column", ReadScalarText("SELECT label_text FROM fluent_row WHERE id = 1"));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-HAS-COLUMN-NAME-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.ColumnBuilder.HasColumnName", "Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions.HasColumnName")]
    public async Task HasColumnName_stores_the_value_under_the_configured_column_asynchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using (var context = CreateContext())
            {
                context.Rows.Add(new FluentRow { Id = 1, Label = "named column", Amount = 2 });
                await context.SaveChangesAsync(ct);
            }

            Assert.Equal("named column", ReadScalarText("SELECT label_text FROM fluent_row WHERE id = 1"));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-IS-REQUIRED-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.IsRequired", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.IsRequired")]
    public async Task IsRequired_rejects_a_null_value_synchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var context = CreateContext();
            context.Rows.Add(new FluentRow { Id = 1, Label = null, Amount = 2 });

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-IS-REQUIRED-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.IsRequired", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.IsRequired")]
    public async Task IsRequired_rejects_a_null_value_asynchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var context = CreateContext();
            context.Rows.Add(new FluentRow { Id = 1, Label = null, Amount = 2 });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(ct));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-HAS-INDEX-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.HasIndex", "Microsoft.EntityFrameworkCore.RelationalIndexBuilderExtensions.HasDatabaseName")]
    public async Task HasIndex_creates_the_named_index_over_the_configured_column_synchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var connection = ProviderTestRuntime.CreateConnection();
            connection.Open();

            var columns = ProviderTestRuntime.IndexMetadataAdapter!.GetIndexColumns(connection, IndexedTable, "ix_fluent_code");

            Assert.Equal(["code"], columns.OrderBy(c => c.Ordinal).Select(c => c.ColumnName));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-FLUENT-HAS-INDEX-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.HasIndex", "Microsoft.EntityFrameworkCore.RelationalIndexBuilderExtensions.HasDatabaseName")]
    public async Task HasIndex_creates_the_named_index_over_the_configured_column_asynchronously()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var connection = ProviderTestRuntime.CreateConnection();
            await connection.OpenAsync(ct);

            var columns = ProviderTestRuntime.IndexMetadataAdapter!.GetIndexColumns(connection, IndexedTable, "ix_fluent_code");

            Assert.Equal(["code"], columns.OrderBy(c => c.Ordinal).Select(c => c.ColumnName));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [CapabilityRequirement("schema-comments")]
    [BehavioralCoverage("EF10-FLUENT-HAS-COMMENT-SYNC", "Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions.HasComment", "Microsoft.EntityFrameworkCore.Metadata.Builders.ColumnBuilder.HasComment")]
    public async Task HasComment_writes_the_column_comment_synchronously()
    {
        ProviderTestRuntime.RequireCapability("schema-comments");
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var connection = ProviderTestRuntime.CreateConnection();
            connection.Open();

            Assert.Equal("the note", ProviderTestRuntime.SchemaCommentMetadataAdapter.GetColumnComment(connection, CommentedTable, "note"));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    [Fact]
    [CapabilityRequirement("schema-comments")]
    [BehavioralCoverage("EF10-FLUENT-HAS-COMMENT-ASYNC", "Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions.HasComment", "Microsoft.EntityFrameworkCore.Metadata.Builders.ColumnBuilder.HasComment")]
    public async Task HasComment_writes_the_column_comment_asynchronously()
    {
        ProviderTestRuntime.RequireCapability("schema-comments");
        var ct = TestContext.Current.CancellationToken;
        await CreateTablesAsync(ct);
        try
        {
            await using var connection = ProviderTestRuntime.CreateConnection();
            await connection.OpenAsync(ct);

            Assert.Equal("the note", ProviderTestRuntime.SchemaCommentMetadataAdapter.GetColumnComment(connection, CommentedTable, "note"));
        }
        finally
        {
            await DropTablesAsync(ct);
        }
    }

    // Helpers. Tables are created from the model's DDL and dropped by name, so the shared database keeps its own tables.

    private static FluentContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FluentContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        return new FluentContext(options.Options);
    }

    private static async Task CreateTablesAsync(CancellationToken ct)
    {
        await DropTablesAsync(ct);
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript(), ct);
    }

    private static async Task DropTablesAsync(CancellationToken ct)
    {
        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(ct);
        foreach (var table in new[] { RowTable, IndexedTable, CommentedTable })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {table}";
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static int CountRows(string table)
    {
        using var connection = ProviderTestRuntime.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? ReadScalarText(string sql)
    {
        using var connection = ProviderTestRuntime.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed class FluentRow
    {
        public int Id { get; set; }
        public string? Label { get; set; }
        public int Amount { get; set; }
    }

    public sealed class FluentIndexed
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
    }

    public sealed class FluentCommented
    {
        public int Id { get; set; }
        public string? Note { get; set; }
    }

    public sealed class FluentContext(DbContextOptions<FluentContext> options) : DbContext(options)
    {
        public DbSet<FluentRow> Rows => Set<FluentRow>();
        public DbSet<FluentIndexed> Indexed => Set<FluentIndexed>();
        public DbSet<FluentCommented> Commented => Set<FluentCommented>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FluentRow>(entity =>
            {
                entity.ToTable(RowTable);
                entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(r => r.Label).HasColumnName("label_text").IsRequired();
                entity.Property(r => r.Amount).HasColumnName("amount");
            });
            modelBuilder.Entity<FluentIndexed>(entity =>
            {
                entity.ToTable(IndexedTable);
                entity.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(i => i.Code).HasColumnName("code");
                entity.HasIndex(i => i.Code).HasDatabaseName("ix_fluent_code");
            });
            modelBuilder.Entity<FluentCommented>(entity =>
            {
                entity.ToTable(CommentedTable);
                entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(c => c.Note).HasColumnName("note").HasComment("the note");
            });
        }
    }
}
