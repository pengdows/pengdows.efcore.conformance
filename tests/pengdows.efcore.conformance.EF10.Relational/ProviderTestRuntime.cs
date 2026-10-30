using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.ComponentModel;
using System.Data.Common;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

internal static class ProviderTestRuntime
{
    private static readonly Lazy<IProviderAdapter> LoadedAdapter = new(LoadAdapter);
    private static readonly Lazy<IReadOnlyList<NativeTypeProbeDefinition>> LoadedNativeTypeProbes = new(BuildNativeTypeProbes);

    public static IProviderAdapter Adapter
    {
        get
        {
            var adapter = LoadedAdapter.Value;
            adapter.Capabilities.Validate();
            return adapter;
        }
    }

    public static string ConnectionString => RequiredEnvironmentVariable("PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING");

    public static IReadOnlyList<NativeTypeProbeDefinition> NativeTypeProbes => LoadedNativeTypeProbes.Value;

    public static string CaseInsensitiveCollation => Adapter is ICollationTestAdapter collationAdapter
        ? collationAdapter.CaseInsensitiveCollation
        : throw new ConformanceInfrastructureException(
            $"Provider '{Adapter.Name}' declares case-insensitive collation support without a collation profile.",
            new InvalidOperationException("The adapter does not implement ICollationTestAdapter."));

    public static IIndexMetadataAdapter IndexMetadataAdapter => Adapter as IIndexMetadataAdapter
        ?? throw new ConformanceInfrastructureException(
            $"Provider '{Adapter.Name}' declares index-sort-metadata support without an index metadata reader.",
            new InvalidOperationException("The adapter does not implement IIndexMetadataAdapter."));

    public static ISchemaCommentMetadataAdapter SchemaCommentMetadataAdapter => Adapter as ISchemaCommentMetadataAdapter
        ?? throw new ConformanceInfrastructureException(
            $"Provider '{Adapter.Name}' declares schema-comments support without a comment metadata reader.",
            new InvalidOperationException("The adapter does not implement ISchemaCommentMetadataAdapter."));

    public static DbConnection CreateConnection() => Adapter is IAdoNetProviderAdapter adoNetAdapter
        ? adoNetAdapter.CreateConnection(ConnectionString)
        : throw new ConformanceInfrastructureException(
            $"Provider '{Adapter.Name}' declared external-connection support but does not expose an ADO.NET connection factory.",
            new InvalidOperationException("The adapter does not implement IAdoNetProviderAdapter."));

    public static ConformanceDbContext CreateContext(Action<DbContextOptionsBuilder<ConformanceDbContext>>? configureOptions = null)
    {
        try
        {
            var options = new DbContextOptionsBuilder<ConformanceDbContext>();
            Adapter.Configure(options, ConnectionString);
            configureOptions?.Invoke(options);
            return new ConformanceDbContext(options.Options);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider adapter configuration failed.", exception);
        }
    }

    public static ConformanceDbContext CreateContext(ParameterTranslationMode collectionMode)
    {
        RequireCapability("parameterized-collection-modes");
        if (Adapter is not IParameterizedCollectionModeAdapter modeAdapter)
        {
            throw new UndeclaredCapabilityException("parameterized-collection-modes");
        }

        try
        {
            var options = new DbContextOptionsBuilder<ConformanceDbContext>();
            modeAdapter.ConfigureParameterizedCollectionMode(options, ConnectionString, collectionMode);
            return new ConformanceDbContext(options.Options);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider parameterized collection mode configuration failed.", exception);
        }
    }

    public static async Task InitializeAsync(ConformanceDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Adapter.InitializeDatabaseAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider database initialization failed.", exception);
        }
    }

    public static async Task<NativeTypeTableLease> CreateNativeTypeTableAsync(
        NativeTypeProbeDefinition probe,
        string tableSuffix,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var schemaContext = CreateContext();
            await Adapter.InitializeDatabaseAsync(schemaContext, cancellationToken);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider database initialization failed.", exception);
        }

        // Database provisioning uses the public EF schema API; the test never inspects the generated script.
        await using var nativeContext = CreateNativeTypeContext(probe, tableSuffix);
        var createScript = nativeContext.Database.GenerateCreateScript();
        try
        {
            await nativeContext.Database.ExecuteSqlRawAsync(createScript, cancellationToken);
        }
        catch
        {
            try
            {
                await CleanupNativeTypeTablesAsync(probe, tableSuffix, CancellationToken.None);
            }
            catch
            {
                // Preserve the creation failure; cleanup is best-effort when creation stopped partway through.
            }

            throw;
        }

        return new NativeTypeTableLease(probe, tableSuffix);
    }

    public static Task CleanupNativeTypeTablesAsync(
        NativeTypeProbeDefinition probe,
        string tableSuffix,
        CancellationToken cancellationToken)
    {
        if (Adapter is not INativeTypeTableCleanupAdapter cleanupAdapter)
        {
            throw new ConformanceInfrastructureException(
                $"Provider '{Adapter.Name}' exposes native type mappings without native test table cleanup.",
                new InvalidOperationException("The adapter does not implement INativeTypeTableCleanupAdapter."));
        }

        return cleanupAdapter.CleanupNativeTypeTablesAsync(
            ConnectionString,
            [$"{probe.NullableTableName}_{tableSuffix}", $"{probe.TableName}_{tableSuffix}"],
            cancellationToken);
    }

    public static void RequireCapability(string feature)
    {
        var capabilities = Adapter.Capabilities;
        if (capabilities.Supports(feature))
        {
            return;
        }

        if (capabilities.UnsupportedFeatures.Contains(feature))
        {
            Assert.Skip($"Provider declares capability '{feature}' unsupported.");
        }

        throw new UndeclaredCapabilityException(feature);
    }

    public static NativeTypeConformanceDbContext CreateNativeTypeContext(NativeTypeProbeDefinition probe, string tableSuffix)
    {
        try
        {
            var options = new DbContextOptionsBuilder<NativeTypeConformanceDbContext>();
            Adapter.Configure(options, ConnectionString);
            options.ReplaceService<IModelCacheKeyFactory, ConformanceModelCacheKeyFactory>();
            return new NativeTypeConformanceDbContext(options.Options, probe, tableSuffix);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider adapter configuration failed.", exception);
        }
    }

    private static IReadOnlyList<NativeTypeProbeDefinition> BuildNativeTypeProbes()
    {
        if (Adapter is not INativeTypeMappingProfile mappingProfile || Adapter is not IAdoNetProviderAdapter adoNetAdapter)
        {
            return Array.Empty<NativeTypeProbeDefinition>();
        }

        using var connection = adoNetAdapter.CreateConnection(ConnectionString);
        var discovery = AdoNetTypeDiscovery.Discover(connection, Adapter as ISupplementalTypeDiscovery);
        var discoveredTypes = discovery.Types
            .Concat(discovery.SupplementalTypes.Where(type => type.IsIndependentDiscovery))
            .ToArray();
        var mappings = mappingProfile.NativeTypeMappings
            .Where(mapping => discoveredTypes.Any(discovered =>
                string.Equals(discovered.DatabaseTypeName, mapping.DatabaseTypeName, StringComparison.OrdinalIgnoreCase) &&
                (mapping.ProviderTypeId is null || string.Equals(discovered.ProviderTypeId, mapping.ProviderTypeId, StringComparison.OrdinalIgnoreCase))))
            .Distinct()
            .ToArray();

        return mappings.Select((mapping, index) => new NativeTypeProbeDefinition(
            index,
            mapping,
            $"ef10_native_type_{index}",
            $"ef10conformance_nativetype_{index}",
            $"ef10_native_type_nullable_{index}",
            $"ef10conformance_nativetype_nullable_{index}")).ToArray();
    }

    private static IProviderAdapter LoadAdapter()
    {
        var assemblyPath = RequiredEnvironmentVariable("PENGDOWS_EFCORE_ADAPTER_ASSEMBLY");
        var typeName = RequiredEnvironmentVariable("PENGDOWS_EFCORE_ADAPTER_TYPE");
        return ProviderAdapterLoader.Load(assemblyPath, typeName);
    }

    private static string RequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {name} to run provider conformance tests.");
}

internal sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();

    public DbSet<KeylessPersonRow> KeylessPeople => Set<KeylessPersonRow>();

    public DbSet<AttributedConfigurationRecord> AttributedConfigurationRecords => Set<AttributedConfigurationRecord>();

    public DbSet<GenericAttributedConfigurationRecord> GenericAttributedConfigurationRecords => Set<GenericAttributedConfigurationRecord>();

    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountSettings> AccountSettings => Set<AccountSettings>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Learner> Learners => Set<Learner>();

    public DbSet<ScalarRecord> ScalarRecords => Set<ScalarRecord>();

    public DbSet<NullableScalarRecord> NullableScalarRecords => Set<NullableScalarRecord>();

    public DbSet<AdditionalClrTypesRecord> AdditionalClrTypesRecords => Set<AdditionalClrTypesRecord>();

    public DbSet<TemporalTypesRecord> TemporalTypesRecords => Set<TemporalTypesRecord>();

    public DbSet<TphItem> TphItems => Set<TphItem>();

    public DbSet<TptItem> TptItems => Set<TptItem>();

    public DbSet<TpcItem> TpcItems => Set<TpcItem>();

    public DbSet<ComplexRecord> ComplexRecords => Set<ComplexRecord>();

    public DbSet<OwnedAttributeRecord> OwnedAttributeRecords => Set<OwnedAttributeRecord>();

    public DbSet<BackingFieldRecord> BackingFieldRecords => Set<BackingFieldRecord>();

    public DbSet<NotifyingRecord> NotifyingRecords => Set<NotifyingRecord>();

    public DbSet<PropertyAccessRecord> PropertyAccessRecords => Set<PropertyAccessRecord>();

    public DbSet<SnapshotStrategyRecord> SnapshotStrategyRecords => Set<SnapshotStrategyRecord>();

    public DbSet<ChangingStrategyRecord> ChangingStrategyRecords => Set<ChangingStrategyRecord>();

    public DbSet<ChangingStrategyWithOriginalsRecord> ChangingStrategyWithOriginalsRecords => Set<ChangingStrategyWithOriginalsRecord>();

    public DbSet<JsonProfileRecord> JsonProfileRecords => Set<JsonProfileRecord>();

    public DbSet<PrimitiveCollectionRecord> PrimitiveCollectionRecords => Set<PrimitiveCollectionRecord>();

    public DbSet<GeneratedKeyRecord> GeneratedKeyRecords => Set<GeneratedKeyRecord>();

    public DbSet<DefaultValueRecord> DefaultValueRecords => Set<DefaultValueRecord>();

    public DbSet<AttributeConfiguredRecord> AttributeConfiguredRecords => Set<AttributeConfiguredRecord>();

    public DbSet<CommentedRecord> CommentedRecords => Set<CommentedRecord>();

    public DbSet<IndexSortRecord> IndexSortRecords => Set<IndexSortRecord>();

    [DbFunction("lower", IsBuiltIn = true, IsNullable = true)]
    public static string? Lower(string? value) => throw new NotSupportedException("This method is intended for use in EF Core queries.");

    [DbFunction("lower", Schema = "pg_catalog", IsBuiltIn = true, IsNullable = true)]
    public static string? LowerInSchema(string? value) => throw new NotSupportedException("This method is intended for use in EF Core queries.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("ef10conformancepeople");
            entity.HasKey(person => person.Id);
            entity.Property(person => person.Id).HasColumnName("id");
            entity.Property(person => person.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
            entity.Property(person => person.OptionalLabel).HasColumnName("optionallabel");
            entity.Property(person => person.Age).HasColumnName("age");
            entity.Property<string>("ShadowLabel").HasColumnName("shadowlabel");
            entity.Property(person => person.OptionalRank).HasColumnName("optionalrank");
            entity.Property(person => person.Version).HasColumnName("version").IsConcurrencyToken();
            entity.Property(person => person.IsDeleted).HasColumnName("isdeleted");
            entity.Property(person => person.IsArchived).HasColumnName("isarchived");
            entity.HasQueryFilter("SoftDelete", person => !person.IsDeleted);
            entity.HasQueryFilter("Archive", person => !person.IsArchived);
        });
        modelBuilder.Entity<UniqueValueRecord>(entity =>
        {
            entity.ToTable("ef10conformanceuniquevalues");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Value).HasColumnName("value").IsRequired().HasMaxLength(160);
        });
        modelBuilder.Entity<CommentedRecord>(entity =>
        {
            entity.ToTable("ef10conformance_commentedrecord");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Value).HasColumnName("value");
        });
        modelBuilder.Entity<IndexSortRecord>(entity =>
        {
            entity.ToTable("ef10conformance_indexsort");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.First).HasColumnName("first");
            entity.Property(record => record.Second).HasColumnName("second");
        });
        modelBuilder.Entity<FilteredUniqueValueRecord>(entity =>
        {
            entity.ToTable("ef10conformance_filtereduniquevalues");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Value).HasColumnName("value").IsRequired().HasMaxLength(160);
            entity.Property(record => record.IsActive).HasColumnName("isactive");
            entity.HasIndex(record => record.Value).IsUnique().HasFilter("isactive = 1");
        });
        modelBuilder.Entity<KeylessPersonRow>(entity =>
        {
            entity.ToTable("ef10conformancekeylesspeople");
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Name).HasColumnName("name");
            entity.Property(row => row.Age).HasColumnName("age");
        });
        modelBuilder.Entity<AttributedConfigurationRecord>();
        modelBuilder.Entity<PrecisionAttributeRecord>(entity =>
        {
            entity.ToTable("ef10conformanceprecisionattribute");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Amount).HasColumnName("amount");
        });
        modelBuilder.Entity<RestrictPrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_restrictprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents)
                .WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId)
                .IsRequired();
        });
        modelBuilder.Entity<RestrictDependent>(entity =>
        {
            entity.ToTable("ef10conformance_restrictdependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<CascadePrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_cascadeprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents)
                .WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId)
                .IsRequired();
        });
        modelBuilder.Entity<CascadeDependent>(entity =>
        {
            entity.ToTable("ef10conformance_cascadedependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<SetNullPrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_setnullprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents)
                .WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId)
                .IsRequired(false);
        });
        modelBuilder.Entity<SetNullDependent>(entity =>
        {
            entity.ToTable("ef10conformance_setnulldependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<ClientCascadePrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_clientcascadeprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents).WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId).IsRequired();
        });
        modelBuilder.Entity<ClientCascadeDependent>(entity =>
        {
            entity.ToTable("ef10conformance_clientcascadedependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<ClientSetNullPrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_clientsetnullprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents)
                .WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId)
                .IsRequired(false);
        });
        modelBuilder.Entity<ClientSetNullDependent>(entity =>
        {
            entity.ToTable("ef10conformance_clientsetnulldependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<NoActionPrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_noactionprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents).WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId).IsRequired();
        });
        modelBuilder.Entity<NoActionDependent>(entity =>
        {
            entity.ToTable("ef10conformance_noactiondependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<ClientNoActionPrincipal>(entity =>
        {
            entity.ToTable("ef10conformance_clientnoactionprincipals");
            entity.HasKey(principal => principal.Id);
            entity.Property(principal => principal.Id).HasColumnName("id");
            entity.HasMany(principal => principal.Dependents).WithOne(dependent => dependent.Principal)
                .HasForeignKey(dependent => dependent.PrincipalId).IsRequired();
        });
        modelBuilder.Entity<ClientNoActionDependent>(entity =>
        {
            entity.ToTable("ef10conformance_clientnoactiondependents");
            entity.HasKey(dependent => dependent.Id);
            entity.Property(dependent => dependent.Id).HasColumnName("id");
            entity.Property(dependent => dependent.PrincipalId).HasColumnName("principalid");
        });
        modelBuilder.Entity<Blog>(entity =>
        {
            entity.ToTable("ef10conformanceblogs");
            entity.HasKey(blog => blog.Id);
            entity.Property(blog => blog.Id).HasColumnName("id");
            entity.Property(blog => blog.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
            entity.HasMany(blog => blog.Posts).WithOne(post => post.Blog).HasForeignKey(post => post.BlogId);
        });
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("ef10conformanceaccounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
            entity.HasOne(account => account.Settings)
                .WithOne(settings => settings.Account)
                .HasForeignKey<AccountSettings>(settings => settings.AccountId)
                .IsRequired();
        });
        modelBuilder.Entity<AccountSettings>(entity =>
        {
            entity.ToTable("ef10conformanceaccountsettings");
            entity.HasKey(settings => settings.AccountId);
            entity.Property(settings => settings.AccountId).HasColumnName("accountid");
            entity.Property(settings => settings.Value).HasColumnName("value").IsRequired().HasMaxLength(160);
        });
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("ef10conformancecourses");
            entity.HasKey(course => course.Id);
            entity.Property(course => course.Id).HasColumnName("id");
            entity.Property(course => course.Title).HasColumnName("title").IsRequired().HasMaxLength(160);
            entity.HasMany(course => course.Learners)
                .WithMany(learner => learner.Courses)
                .UsingEntity(join => join.ToTable("ef10conformancecourselearners"));
        });
        modelBuilder.Entity<Learner>(entity =>
        {
            entity.ToTable("ef10conformancelearners");
            entity.HasKey(learner => learner.Id);
            entity.Property(learner => learner.Id).HasColumnName("id");
            entity.Property(learner => learner.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
        });
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("ef10conformanceposts");
            entity.HasKey(post => post.Id);
            entity.Property(post => post.Id).HasColumnName("id");
            entity.Property(post => post.BlogId).HasColumnName("blogid");
            entity.Property(post => post.Title).HasColumnName("title").IsRequired().HasMaxLength(160);
            entity.HasMany(post => post.Comments).WithOne(comment => comment.Post).HasForeignKey(comment => comment.PostId);
        });
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("ef10conformancecomments");
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.Id).HasColumnName("id");
            entity.Property(comment => comment.PostId).HasColumnName("postid");
            entity.Property(comment => comment.Text).HasColumnName("text").IsRequired().HasMaxLength(160);
        });
        modelBuilder.Entity<ScalarRecord>(entity =>
        {
            entity.ToTable("ef10conformancescalars");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Amount).HasColumnName("amount").HasPrecision(18, 4);
            entity.Property(record => record.ConvertedAmount).HasColumnName("convertedamount").HasConversion<string>();
            entity.Property(record => record.Day).HasColumnName("day");
            entity.Property(record => record.Start).HasColumnName("start").HasPrecision(6);
            entity.Property(record => record.Enabled).HasColumnName("enabled");
            entity.Property(record => record.Payload).HasColumnName("payload");
            entity.Property(record => record.Mood).HasColumnName("mood").HasConversion<string>();
        });
        modelBuilder.Entity<NullableScalarRecord>(entity =>
        {
            entity.ToTable("ef10conformancenullablescalars");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Integer).HasColumnName("integer_value");
            entity.Property(record => record.Flag).HasColumnName("flag");
            entity.Property(record => record.Amount).HasColumnName("amount").HasPrecision(18, 4);
            entity.Property(record => record.Text).HasColumnName("text_value").HasMaxLength(160);
            entity.Property(record => record.Day).HasColumnName("day");
            entity.Property(record => record.Time).HasColumnName("time_value").HasPrecision(6);
            entity.Property(record => record.Payload).HasColumnName("payload");
            entity.Property(record => record.Token).HasColumnName("token");
            entity.Property(record => record.Duration).HasColumnName("duration");
            entity.Property(record => record.DateTimeValue).HasColumnName("datetime_value").HasPrecision(6);
            entity.Property(record => record.OffsetDateTimeValue).HasColumnName("offset_datetime_value").HasPrecision(6);
        });
        modelBuilder.Entity<AdditionalClrTypesRecord>(entity =>
        {
            entity.ToTable("ef10conformanceadditionaltypes");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.ByteValue).HasColumnName("bytevalue");
            entity.Property(record => record.Int16Value).HasColumnName("int16value");
            entity.Property(record => record.Int64Value).HasColumnName("int64value");
            entity.Property(record => record.SingleValue).HasColumnName("singlevalue");
            entity.Property(record => record.DoubleValue).HasColumnName("doublevalue");
            entity.Property(record => record.CharacterValue).HasColumnName("charactervalue");
            entity.Property(record => record.DurationValue).HasColumnName("durationvalue");
        });
        modelBuilder.Entity<TemporalTypesRecord>(entity =>
        {
            entity.ToTable("ef10conformancetemporaltypes");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.DateTimeValue).HasColumnName("localdatetime").HasPrecision(6);
            entity.Property(record => record.UtcDateTime).HasColumnName("utcdatetime").HasPrecision(6);
            entity.Property(record => record.OffsetDateTime).HasColumnName("offsetdatetime").HasPrecision(6);
        });
        modelBuilder.Entity<TphItem>(entity =>
        {
            entity.ToTable("ef10conformancetph");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").IsRequired();
            entity.HasDiscriminator<string>("kind")
                .HasValue<TphArticle>("article")
                .HasValue<TphVideo>("video");
        });
        modelBuilder.Entity<TptItem>(entity =>
        {
            entity.UseTptMappingStrategy();
            entity.ToTable("ef10conformancetptbase");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").IsRequired();
        });
        modelBuilder.Entity<TptArticle>().ToTable("ef10conformancetptarticle");
        modelBuilder.Entity<TptVideo>().ToTable("ef10conformancetptvideo");
        modelBuilder.Entity<TpcItem>(entity =>
        {
            entity.UseTpcMappingStrategy();
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").IsRequired();
        });
        modelBuilder.Entity<TpcArticle>().ToTable("ef10conformancetpcarticle");
        modelBuilder.Entity<TpcVideo>().ToTable("ef10conformancetpcvideo");
        modelBuilder.Entity<ComplexRecord>(entity =>
        {
            entity.ToTable("ef10conformancecomplex");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.ComplexProperty(record => record.Address, address =>
            {
                address.Property(value => value.Street).HasColumnName("address_street").IsRequired();
                address.Property(value => value.City).HasColumnName("address_city").IsRequired();
            });
        });
        modelBuilder.Entity<OwnedAttributeRecord>(entity =>
        {
            entity.ToTable("ef10conformance_ownedattribute");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
        });
        modelBuilder.Entity<BackingFieldRecord>(entity =>
        {
            entity.ToTable("ef10conformance_backingfield");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Label).HasColumnName("label");
        });
        modelBuilder.Entity<NotifyingRecord>(entity =>
        {
            entity.ToTable("ef10conformance_notifyingrecords");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Name).HasColumnName("name");
            entity.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangedNotifications);
        });
        modelBuilder.Entity<PropertyAccessRecord>(entity =>
        {
            entity.ToTable("ef10conformance_propertyaccess");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Ignore(record => record.SetterCalls);
            entity.Property(record => record.Field).HasColumnName("field").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Property(record => record.FieldDuringConstruction).HasColumnName("fieldduringconstruction").UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
            entity.Property(record => record.Property).HasColumnName("property").UsePropertyAccessMode(PropertyAccessMode.Property);
            entity.Property(record => record.PreferField).HasColumnName("preferfield").UsePropertyAccessMode(PropertyAccessMode.PreferField);
            entity.Property(record => record.PreferFieldDuringConstruction).HasColumnName("preferfieldduringconstruction").UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
            entity.Property(record => record.PreferProperty).HasColumnName("preferproperty").UsePropertyAccessMode(PropertyAccessMode.PreferProperty);
        });
        modelBuilder.Entity<SnapshotStrategyRecord>(entity =>
        {
            entity.ToTable("ef10conformance_snapshotstrategy");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Name).HasColumnName("name");
            entity.HasChangeTrackingStrategy(ChangeTrackingStrategy.Snapshot);
        });
        modelBuilder.Entity<ChangingStrategyRecord>(entity =>
        {
            entity.ToTable("ef10conformance_changingstrategy");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Name).HasColumnName("name");
            entity.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotifications);
        });
        modelBuilder.Entity<ChangingStrategyWithOriginalsRecord>(entity =>
        {
            entity.ToTable("ef10conformance_changingstrategyoriginals");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Name).HasColumnName("name");
            entity.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);
        });
        if (ProviderTestRuntime.Adapter.Capabilities.Supports("json-mapping"))
        {
            modelBuilder.Entity<JsonProfileRecord>(entity =>
            {
                entity.ToTable("ef10conformance_jsonprofiles");
                entity.HasKey(record => record.Id);
                entity.Property(record => record.Id).HasColumnName("id");
                entity.OwnsOne(record => record.Profile, profile =>
                {
                    profile.ToJson("profile");
                    profile.OwnsOne(value => value.Preferences);
                });
            });
        }
        else
        {
            modelBuilder.Ignore<JsonProfileRecord>();
        }
        modelBuilder.Entity<PrimitiveCollectionRecord>(entity =>
        {
            entity.ToTable("ef10conformanceprimitivecollections");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Values).HasColumnName("values");
        });
        modelBuilder.Entity<GeneratedKeyRecord>(entity =>
        {
            entity.ToTable("ef10conformancegeneratedkeys");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(record => record.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
        });
        modelBuilder.Entity<DefaultValueRecord>(entity =>
        {
            entity.ToTable("ef10conformancedefaultvalues");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.RetryCount).HasColumnName("retry_count").HasDefaultValue(3);
            entity.Property(record => record.Enabled).HasColumnName("enabled").HasDefaultValue(true).HasSentinel(false);
            entity.Property(record => record.Label).HasColumnName("label").HasMaxLength(160).HasDefaultValue("database default");
        });
        modelBuilder.Entity<AttributeConfiguredRecord>();
    }

}

internal interface INativeTypeModelContext
{
    NativeTypeProbeDefinition Probe { get; }

    string TableSuffix { get; }
}

internal sealed class NativeTypeConformanceDbContext(
    DbContextOptions<NativeTypeConformanceDbContext> options,
    NativeTypeProbeDefinition probe,
    string tableSuffix) : DbContext(options), INativeTypeModelContext
{
    public NativeTypeProbeDefinition Probe { get; } = probe;

    public string TableSuffix { get; } = tableSuffix;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.SharedTypeEntity<Dictionary<string, object>>(Probe.EntityTypeName);
        entity.ToTable($"{Probe.TableName}_{TableSuffix}");
        entity.IndexerProperty<Guid>("id").HasColumnName("id");
        entity.HasKey("id");
        entity.IndexerProperty(Probe.Mapping.ClrType, "value")
            .HasColumnName("value")
            .HasColumnType(Probe.Mapping.StoreTypeName ?? Probe.Mapping.DatabaseTypeName);

        var nullableClrType = Probe.Mapping.ClrType.IsValueType && Nullable.GetUnderlyingType(Probe.Mapping.ClrType) is null
            ? typeof(Nullable<>).MakeGenericType(Probe.Mapping.ClrType)
            : Probe.Mapping.ClrType;
        var nullableEntity = modelBuilder.SharedTypeEntity<Dictionary<string, object>>(Probe.NullableEntityTypeName);
        nullableEntity.ToTable($"{Probe.NullableTableName}_{TableSuffix}");
        nullableEntity.IndexerProperty<Guid>("id").HasColumnName("id");
        nullableEntity.HasKey("id");
        nullableEntity.IndexerProperty(nullableClrType, "value")
            .HasColumnName("value")
            .HasColumnType(Probe.Mapping.StoreTypeName ?? Probe.Mapping.DatabaseTypeName)
            .IsRequired(false);
    }
}

internal sealed class NativeTypeTableLease(NativeTypeProbeDefinition probe, string tableSuffix) : IAsyncDisposable
{
    public NativeTypeConformanceDbContext CreateContext() => ProviderTestRuntime.CreateNativeTypeContext(probe, tableSuffix);

    public async ValueTask DisposeAsync()
    {
        await ProviderTestRuntime.CleanupNativeTypeTablesAsync(probe, tableSuffix, CancellationToken.None);
    }
}

/// <summary>Allows each discovered native type test to validate its own model without masking unrelated mappings.</summary>
internal sealed class ConformanceModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is INativeTypeModelContext nativeContext
        ? (context.GetType(), nativeContext.Probe.Index, nativeContext.TableSuffix, designTime)
        : (context.GetType(), designTime);
}

internal sealed class Person
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? OptionalLabel { get; set; }

    public int Age { get; set; }

    public int? OptionalRank { get; set; }

    public int Version { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsArchived { get; set; }
}

[Keyless]
internal sealed class KeylessPersonRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}

[EntityTypeConfiguration(typeof(AttributedConfigurationRecordConfiguration))]
internal sealed class AttributedConfigurationRecord
{
    public Guid Id { get; set; }

    public string Value { get; set; } = string.Empty;
}

internal sealed class AttributedConfigurationRecordConfiguration : IEntityTypeConfiguration<AttributedConfigurationRecord>
{
    public void Configure(EntityTypeBuilder<AttributedConfigurationRecord> builder)
    {
        builder.ToTable("ef10conformance_entityconfiguration");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.Value).HasColumnName("value");
    }
}

[EntityTypeConfiguration<GenericAttributedConfigurationRecordConfiguration, GenericAttributedConfigurationRecord>]
internal sealed class GenericAttributedConfigurationRecord
{
    public Guid Id { get; set; }

    public string Value { get; set; } = string.Empty;
}

internal sealed class GenericAttributedConfigurationRecordConfiguration : IEntityTypeConfiguration<GenericAttributedConfigurationRecord>
{
    public void Configure(EntityTypeBuilder<GenericAttributedConfigurationRecord> builder)
    {
        builder.ToTable("ef10conformancegenericattributed");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("configured_id");
        builder.Property(record => record.Value).HasColumnName("configured_value");
    }
}

internal sealed class Blog
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

internal sealed class Account
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public AccountSettings? Settings { get; set; }
}

internal sealed class AccountSettings
{
    public Guid AccountId { get; set; }

    public string Value { get; set; } = string.Empty;

    public Account Account { get; set; } = null!;
}

internal sealed class Course
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public ICollection<Learner> Learners { get; set; } = new List<Learner>();
}

internal sealed class Learner
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

internal sealed class Post
{
    public Guid Id { get; set; }

    public Guid BlogId { get; set; }

    public string Title { get; set; } = string.Empty;

    public Blog? Blog { get; set; }

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

internal sealed class Comment
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public string Text { get; set; } = string.Empty;

    public Post? Post { get; set; }
}

[Index(nameof(Value), IsUnique = true, Name = "IX_EF10_UniqueValueRecord_Value")]
internal sealed class UniqueValueRecord
{
    public Guid Id { get; set; }

    public string Value { get; set; } = string.Empty;
}

[Index(nameof(First), nameof(Second), Name = "IX_EF10_IndexSort_Mixed", IsDescending = new bool[] { true, false })]
[Index(nameof(First), nameof(Second), Name = "IX_EF10_IndexSort_AllDescending", AllDescending = true)]
internal sealed class IndexSortRecord
{
    public Guid Id { get; set; }

    public string First { get; set; } = string.Empty;

    public string Second { get; set; } = string.Empty;
}

[Comment("EF10 conformance table comment")]
internal sealed class CommentedRecord
{
    public Guid Id { get; set; }

    [Comment("EF10 conformance column comment")]
    public string Value { get; set; } = string.Empty;
}

internal sealed class FilteredUniqueValueRecord
{
    public Guid Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public int IsActive { get; set; }
}

internal sealed class PrecisionAttributeRecord
{
    public Guid Id { get; set; }

    [Precision(18, 4)]
    public decimal Amount { get; set; }

}

internal sealed class RestrictPrincipal
{
    public Guid Id { get; set; }

    public ICollection<RestrictDependent> Dependents { get; set; } = new List<RestrictDependent>();
}

internal sealed class RestrictDependent
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public RestrictPrincipal? Principal { get; set; }
}

internal sealed class CascadePrincipal
{
    public Guid Id { get; set; }

    public ICollection<CascadeDependent> Dependents { get; set; } = new List<CascadeDependent>();
}

internal sealed class CascadeDependent
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public CascadePrincipal? Principal { get; set; }
}

internal sealed class SetNullPrincipal
{
    public Guid Id { get; set; }

    public ICollection<SetNullDependent> Dependents { get; set; } = new List<SetNullDependent>();
}

internal sealed class SetNullDependent
{
    public Guid Id { get; set; }

    public Guid? PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.SetNull)]
    public SetNullPrincipal? Principal { get; set; }
}

internal sealed class ClientCascadePrincipal
{
    public Guid Id { get; set; }

    public ICollection<ClientCascadeDependent> Dependents { get; set; } = new List<ClientCascadeDependent>();
}

internal sealed class ClientCascadeDependent
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.ClientCascade)]
    public ClientCascadePrincipal? Principal { get; set; }
}

internal sealed class ClientSetNullPrincipal
{
    public Guid Id { get; set; }

    public ICollection<ClientSetNullDependent> Dependents { get; set; } = new List<ClientSetNullDependent>();
}

internal sealed class ClientSetNullDependent
{
    public Guid Id { get; set; }

    public Guid? PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.ClientSetNull)]
    public ClientSetNullPrincipal? Principal { get; set; }
}

internal sealed class NoActionPrincipal
{
    public Guid Id { get; set; }

    public ICollection<NoActionDependent> Dependents { get; set; } = new List<NoActionDependent>();
}

internal sealed class NoActionDependent
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.NoAction)]
    public NoActionPrincipal? Principal { get; set; }
}

internal sealed class ClientNoActionPrincipal
{
    public Guid Id { get; set; }

    public ICollection<ClientNoActionDependent> Dependents { get; set; } = new List<ClientNoActionDependent>();
}

internal sealed class ClientNoActionDependent
{
    public Guid Id { get; set; }

    public Guid PrincipalId { get; set; }

    [DeleteBehavior(DeleteBehavior.ClientNoAction)]
    public ClientNoActionPrincipal? Principal { get; set; }
}

[System.ComponentModel.DataAnnotations.Schema.Table("ef10conformance_attributeconfigured")]
[PrimaryKey(nameof(Key))]
internal sealed class AttributeConfiguredRecord
{
    [System.ComponentModel.DataAnnotations.Schema.Column("recordkey")]
    public Guid Key { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("displayname")]
    [Unicode(false)]
    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class ScalarRecord
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public decimal ConvertedAmount { get; set; }

    public DateOnly Day { get; set; }

    public TimeOnly Start { get; set; }

    public bool Enabled { get; set; }

    public byte[] Payload { get; set; } = [];

    public TestMood Mood { get; set; }
}

internal sealed class NullableScalarRecord
{
    public Guid Id { get; set; }

    public int? Integer { get; set; }

    public bool? Flag { get; set; }

    public decimal? Amount { get; set; }

    public string? Text { get; set; }

    public DateOnly? Day { get; set; }

    public TimeOnly? Time { get; set; }

    public byte[]? Payload { get; set; }

    public Guid? Token { get; set; }

    public TimeSpan? Duration { get; set; }

    public DateTime? DateTimeValue { get; set; }

    public DateTimeOffset? OffsetDateTimeValue { get; set; }
}

internal sealed class AdditionalClrTypesRecord
{
    public Guid Id { get; set; }

    public byte ByteValue { get; set; }

    public short Int16Value { get; set; }

    public long Int64Value { get; set; }

    public float SingleValue { get; set; }

    public double DoubleValue { get; set; }

    public char CharacterValue { get; set; }

    public TimeSpan DurationValue { get; set; }
}

internal sealed class TemporalTypesRecord
{
    public Guid Id { get; set; }

    public DateTime DateTimeValue { get; set; }

    public DateTime UtcDateTime { get; set; }

    public DateTimeOffset OffsetDateTime { get; set; }
}

internal enum TestMood
{
    Calm,
    Curious
}

internal abstract class TphItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TphArticle : TphItem
{
    public string Body { get; set; } = string.Empty;
}

internal sealed class TphVideo : TphItem
{
    public int DurationSeconds { get; set; }
}

internal abstract class TptItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TptArticle : TptItem
{
    public string Body { get; set; } = string.Empty;
}

internal sealed class TptVideo : TptItem
{
    public int DurationSeconds { get; set; }
}

internal abstract class TpcItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TpcArticle : TpcItem
{
    public string Body { get; set; } = string.Empty;
}

internal sealed class TpcVideo : TpcItem
{
    public int DurationSeconds { get; set; }
}

internal sealed class ComplexRecord
{
    public Guid Id { get; set; }

    public AddressInfo Address { get; set; } = new();
}

internal sealed class JsonProfileRecord
{
    public Guid Id { get; set; }

    public JsonProfile Profile { get; set; } = new();
}

internal sealed class JsonProfile
{
    public string Biography { get; set; } = string.Empty;

    public JsonPreferences Preferences { get; set; } = new();
}

internal sealed class JsonPreferences
{
    public bool EmailNotifications { get; set; }
}

internal sealed class OwnedAttributeRecord
{
    public Guid Id { get; set; }

    public OwnedAttributeValue Value { get; set; } = new();
}

[Owned]
internal sealed class OwnedAttributeValue
{
    public string Label { get; set; } = string.Empty;

    public int Rank { get; set; }
}

internal sealed class BackingFieldRecord
{
    private string _label = string.Empty;

    public Guid Id { get; set; }

    [BackingField(nameof(_label))]
    public string Label => _label;

    public void ChangeLabel(string value) => _label = value;
}

internal sealed class NotifyingRecord : INotifyPropertyChanged
{
    private string _name = string.Empty;

    public Guid Id { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed class PropertyAccessRecord
{
    private string _field = string.Empty;
    private string _fieldDuringConstruction = string.Empty;
    private string _property = string.Empty;
    private string _preferField = string.Empty;
    private string _preferFieldDuringConstruction = string.Empty;
    private string _preferProperty = string.Empty;

    public Guid Id { get; set; }

    public int SetterCalls { get; private set; }

    public string Field { get => _field; set { _field = value; SetterCalls++; } }

    public string FieldDuringConstruction { get => _fieldDuringConstruction; set { _fieldDuringConstruction = value; SetterCalls++; } }

    public string Property { get => _property; set { _property = value; SetterCalls++; } }

    public string PreferField { get => _preferField; set { _preferField = value; SetterCalls++; } }

    public string PreferFieldDuringConstruction { get => _preferFieldDuringConstruction; set { _preferFieldDuringConstruction = value; SetterCalls++; } }

    public string PreferProperty { get => _preferProperty; set { _preferProperty = value; SetterCalls++; } }

    public void ResetSetterCalls() => SetterCalls = 0;
}

internal sealed class SnapshotStrategyRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class ChangingStrategyRecord : INotifyPropertyChanging, INotifyPropertyChanged
{
    private string _name = string.Empty;

    public Guid Id { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Name)));
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public event PropertyChangingEventHandler? PropertyChanging;

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed class ChangingStrategyWithOriginalsRecord : INotifyPropertyChanging, INotifyPropertyChanged
{
    private string _name = string.Empty;

    public Guid Id { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Name)));
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public event PropertyChangingEventHandler? PropertyChanging;

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal sealed class AddressInfo
{
    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;
}

internal sealed class PrimitiveCollectionRecord
{
    public Guid Id { get; set; }

    public List<int> Values { get; set; } = [];
}

internal sealed class GeneratedKeyRecord
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class DefaultValueRecord
{
    public Guid Id { get; set; }

    public int RetryCount { get; set; }

    public bool Enabled { get; set; }

    public string Label { get; set; } = null!;
}

internal sealed record NativeTypeProbeDefinition(
    int Index,
    NativeTypeMapping Mapping,
    string EntityTypeName,
    string TableName,
    string NullableEntityTypeName,
    string NullableTableName);
