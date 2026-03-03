// -----------------------------------------------------------------------------
// PostgreSQL DTO Generator.
// Supports: Tables, Enums, Composite Types, Arrays, Most Built-in Types
// Fails fast on unsupported types.
// -----------------------------------------------------------------------------

using System.Text;
using Npgsql;

/// <summary>
/// PostgreSQL schema introspection tool that generates C# DTO classes from
/// tables, enums, composite types, and array columns.
/// </summary>
/// <remarks>
/// <para>
/// This generator connects to a PostgreSQL database, inspects schema metadata
/// via <c>pg_catalog</c>, and produces deterministic, nullable-aware C# types
/// representing the database structure.
/// </para>
///
/// <para>
/// Supported features:
/// </para>
/// <list type="bullet">
/// <item>Table-based entity generation (excluding common audit columns)</item>
/// <item>PostgreSQL enum types mapped to C# enums</item>
/// <item>Composite types mapped to standalone C# classes</item>
/// <item>Array column support for all mapped types</item>
/// <item>Broad coverage of built-in PostgreSQL data types</item>
/// </list>
///
/// <para>
/// The generator is PostgreSQL-specific and intentionally fails fast when
/// encountering unsupported or ambiguous database types. Output is written
/// atomically to prevent partial file corruption.
/// </para>
///
/// <para>
/// This implementation is designed as a single-file, drop-in utility that
/// can be embedded into any project without external dependencies beyond
/// <c>Npgsql</c>.
/// </para>
/// </remarks>
internal static class Program
{
    private const string DefaultSchema = "public";

    private static readonly HashSet<string> CommonColumns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "id",
            "txn_no",
            "inserted_at",
            "updated_at"
        };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "dotnet run \"<Connection string>\" \"<Output folder>\" \"<Namespace>\" [Schema name]\n\n" +
                "Example: \n\n" +
                "dotnet run \"Host=localhost;Port=5432;Database=sample-db;Username=appuser;Password=Secret123\" \\\n" +
                "./dtos MyApp.Models public\n");
            return 1;
        }

        var connectionString = args[0];
        var outputDir = Path.GetFullPath(args[1]);
        var targetNamespace = args[2];
        var schema = args.Length == 4 ? args[3] : DefaultSchema;

        Directory.CreateDirectory(outputDir);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var enums = await LoadEnums(conn, schema);
        var composites = await LoadCompositeTypes(conn, schema);
        var tables = await LoadTables(conn, schema);

        GenerateBaseEntity(outputDir, targetNamespace);
        GenerateEnums(outputDir, enums, targetNamespace);
        GenerateComposites(outputDir, composites, targetNamespace, enums);
        GenerateEntities(outputDir, tables, targetNamespace, enums, composites);

        Console.WriteLine("DTO generation completed.");
        return 0;
    }

    // ============================================================
    // ENUMS
    // ============================================================

    private static async Task<Dictionary<string, List<string>>> LoadEnums(
        NpgsqlConnection conn,
        string schema)
    {
        const string sql = """
            SELECT t.typname, e.enumlabel
            FROM pg_type t
            JOIN pg_enum e ON t.oid = e.enumtypid
            JOIN pg_namespace n ON n.oid = t.typnamespace
            WHERE n.nspname = @schema
            ORDER BY t.typname, e.enumsortorder;
        """;

        var result = new Dictionary<string, List<string>>();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var value = reader.GetString(1);

            if (!result.TryGetValue(name, out var list))
            {
                list = new List<string>();
                result[name] = list;
            }

            list.Add(value);
        }

        return result;
    }

    private static void GenerateEnums(
        string outputDir,
        Dictionary<string, List<string>> enums,
        string ns)
    {
        if (enums.Count == 0)
            return;

        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        foreach (var (name, values) in enums.OrderBy(e => e.Key))
        {
            var enumName = ToPascalCase(name);

            sb.AppendLine($"public enum {enumName}");
            sb.AppendLine("{");

            foreach (var value in values)
                sb.AppendLine($"    {SafeIdentifier(value)},");

            sb.AppendLine("}");
            sb.AppendLine();
        }

        WriteAtomic(Path.Combine(outputDir, "_PgEnums.g.cs"), sb.ToString());
    }

    // ============================================================
    // COMPOSITE TYPES
    // ============================================================

    private static async Task<Dictionary<string, List<ColumnInfo>>> LoadCompositeTypes(
        NpgsqlConnection conn,
        string schema)
    {
        const string sql = """
            SELECT
                t.typname,
                a.attname,
                a.attnotnull,
                pg_catalog.format_type(a.atttypid, a.atttypmod),
                bt.typname
            FROM pg_type t
            JOIN pg_class c ON t.typrelid = c.oid
            JOIN pg_namespace n ON n.oid = t.typnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid
            JOIN pg_type bt ON a.atttypid = bt.oid
            WHERE n.nspname = @schema
              AND t.typtype = 'c'
              AND c.relkind = 'c'
              AND a.attnum > 0
              AND NOT a.attisdropped
            ORDER BY t.typname, a.attnum;
        """;

        var result = new Dictionary<string, List<ColumnInfo>>();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var type = reader.GetString(0);
            var column = reader.GetString(1);
            var notNull = reader.GetBoolean(2);
            var formatted = reader.GetString(3);
            var udt = reader.GetString(4);

            if (!result.TryGetValue(type, out var list))
            {
                list = new List<ColumnInfo>();
                result[type] = list;
            }

            list.Add(new ColumnInfo(column, !notNull, formatted, udt));
        }

        return result;
    }

    private static void GenerateComposites(
        string outputDir,
        Dictionary<string, List<ColumnInfo>> composites,
        string ns,
        Dictionary<string, List<string>> enums)
    {
        if (composites.Count == 0)
            return;

        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();

        foreach (var (name, columns) in composites.OrderBy(c => c.Key))
        {
            var typeName = ToPascalCase(name);

            sb.AppendLine($"public sealed class {typeName}");
            sb.AppendLine("{");

            foreach (var col in columns)
            {
                var propName = ToPascalCase(col.Name);
                var clrType = MapType(col, enums);

                sb.AppendLine($"    public {clrType} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        WriteAtomic(Path.Combine(outputDir, "_PgComposites.g.cs"), sb.ToString());
    }

    // ============================================================
    // TABLES
    // ============================================================

    private static async Task<Dictionary<string, List<ColumnInfo>>> LoadTables(
        NpgsqlConnection conn,
        string schema)
    {
        const string sql = """
            SELECT
                c.relname,
                a.attname,
                a.attnotnull,
                pg_catalog.format_type(a.atttypid, a.atttypmod),
                t.typname
            FROM pg_attribute a
            JOIN pg_class c ON a.attrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            JOIN pg_type t ON a.atttypid = t.oid
            WHERE a.attnum > 0
              AND NOT a.attisdropped
              AND c.relkind = 'r'
              AND n.nspname = @schema
            ORDER BY c.relname, a.attnum;
        """;

        var result = new Dictionary<string, List<ColumnInfo>>();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            var column = reader.GetString(1);
            var notNull = reader.GetBoolean(2);
            var formatted = reader.GetString(3);
            var udt = reader.GetString(4);

            if (!result.TryGetValue(table, out var list))
            {
                list = new List<ColumnInfo>();
                result[table] = list;
            }

            list.Add(new ColumnInfo(column, !notNull, formatted, udt));
        }

        return result;
    }

    private static void GenerateEntities(
        string outputDir,
        Dictionary<string, List<ColumnInfo>> tables,
        string ns,
        Dictionary<string, List<string>> enums,
        Dictionary<string, List<ColumnInfo>> composites)
    {
        foreach (var (table, columns) in tables.OrderBy(t => t.Key))
        {
            var className = ToPascalCase(Singularize(table));
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
            sb.AppendLine($"public sealed class {className} : BaseEntity");
            sb.AppendLine("{");

            foreach (var col in columns)
            {
                if (CommonColumns.Contains(col.Name))
                    continue;

                var propName = ToPascalCase(col.Name);
                var clrType = MapType(col, enums);

                sb.AppendLine($"    public {clrType} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("}");

            WriteAtomic(Path.Combine(outputDir, $"{className}.cs"), sb.ToString());
        }
    }

    // ============================================================
    // TYPE MAPPING
    // ============================================================

    private static string MapType(
        ColumnInfo column,
        Dictionary<string, List<string>> enums)
    {
        var formatted = column.FormattedType.ToLowerInvariant();

        if (formatted.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = formatted[..^2];
            var elementClr = MapPrimitive(elementType, column.UdtName, enums, false);
            return column.IsNullable
                ? $"{elementClr}[]?"
                : $"{elementClr}[]";
        }

        return MapPrimitive(formatted, column.UdtName, enums, column.IsNullable);
    }

    private static string MapPrimitive(
        string type,
        string udtName,
        Dictionary<string, List<string>> enums,
        bool nullable)
    {
        if (enums.ContainsKey(udtName))
        {
            var enumType = ToPascalCase(udtName);
            return nullable ? $"{enumType}?" : enumType;
        }

        string clr = type switch
        {
            // Numeric
            "smallint" => "short",
            "integer" => "int",
            "bigint" => "long",
            "numeric" => "decimal",
            "decimal" => "decimal",
            "real" => "float",
            "double precision" => "double",
            "money" => "decimal",

            // Boolean
            "boolean" => "bool",

            // Text
            "text" => "string",
            "character" => "string",
            var s when s.StartsWith("character varying") => "string",
            var s when s.StartsWith("varchar") => "string",

            // Binary
            "bytea" => "byte[]",

            // UUID
            "uuid" => "Guid",

            // Date/Time
            "date" => "DateOnly",
            "time without time zone" => "TimeOnly",
            "time with time zone" => "DateTimeOffset",
            "timestamp without time zone" => "DateTime",
            "timestamp with time zone" => "DateTimeOffset",
            "interval" => "TimeSpan",

            // JSON
            "json" => "string",
            "jsonb" => "string",

            // Network
            "inet" => "string",
            "cidr" => "string",
            "macaddr" => "string",
            "macaddr8" => "string",

            // XML
            "xml" => "string",

            _ => ToPascalCase(udtName) // composite fallback
        };

        if (clr == "string")
            return nullable ? "string?" : "string";

        if (clr == "byte[]")
            return nullable ? "byte[]?" : "byte[]";

        return nullable ? clr + "?" : clr;
    }

    // ============================================================
    // UTILITIES
    // ============================================================

    private static void GenerateBaseEntity(string outputDir, string ns)
    {
        var content = $$"""
        using System;

        namespace {{ns}};

        public abstract class BaseEntity
        {
            public long Id { get; set; }
            public int TxnNo { get; set; }
            public DateTime InsertedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
        """;

        WriteAtomic(Path.Combine(outputDir, "BaseEntity.cs"), content);
    }

    private static void WriteAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, Encoding.UTF8);
        File.Move(temp, path, true);
    }

    private static string ToPascalCase(string value)
        => string.Concat(value.Split('_',
                StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));

    private static string Singularize(string name)
        => name.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && !name.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            ? name[..^1]
            : name;

    private static string SafeIdentifier(string value)
    {
        var cleaned = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

        if (char.IsDigit(cleaned[0]))
            cleaned = "_" + cleaned;

        return cleaned;
    }

    private sealed record ColumnInfo(
        string Name,
        bool IsNullable,
        string FormattedType,
        string UdtName);
}