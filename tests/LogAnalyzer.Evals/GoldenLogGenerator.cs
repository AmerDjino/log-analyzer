using System.Globalization;
using System.Text;

namespace LogAnalyzer.Evals;

/// <summary>
/// Writes the golden logs and their eval cases. Each log is built from a fixed seed, so the same code always writes the
/// same files, and its planted incident sits at lines the case's graders name. The output is committed:
/// <see cref="GoldenLogTests"/> checks that it still matches, and rewrites it when UPDATE_GOLDEN_LOGS=1.
/// </summary>
public static class GoldenLogGenerator
{
    private const string Capability = "log-analysis";

    /// <summary>Every case: its scenario id, its log's file name and content, and the case file's content.</summary>
    public static IReadOnlyList<GoldenCase> Generate() =>
    [
        PlantedExceptionBurst(),
        ConnectionPoolExhaustion(),
        ProcessCrash(),
        SlowQueriesWithoutFailures(),
        RepeatedTimeout(),
        CleanLog(),
    ];

    // 600 lines; 40 NullReferenceException entries among lines 200 to 260, the first on 200 and the last on 260.
    private static GoldenCase PlantedExceptionBurst()
    {
        var log = new LogWriter(seed: 1);
        log.Startup();
        log.RoutineUntil(199);
        var burst = new HashSet<int> { 200, 260 };
        while (burst.Count < 40)
        {
            burst.Add(log.Next(201, 260));
        }

        for (var line = 200; line <= 260; line++)
        {
            if (burst.Contains(line))
            {
                log.Add(
                    "ERR",
                    "Orders.Api.Pricing.PricingService",
                    $"Failed to price order {log.Next(10_000, 99_999)}. System.NullReferenceException: Object reference not set to an "
                    + "instance of an object. at Orders.Api.Pricing.PricingService.ApplyDiscount(Order order) in "
                    + "/src/Orders.Api/Pricing/PricingService.cs:line 87");
            }
            else
            {
                log.Routine();
            }
        }

        log.RoutineUntil(600);
        return log.Case(
            "planted-exception-burst-is-reported",
            Findings([(200, 260)], categories: ["exception"], severities: ["error", "critical"], min: 1));
    }

    // 5,000 lines; from line 4,120 the connection pool times out and requests fail with 503.
    private static GoldenCase ConnectionPoolExhaustion()
    {
        var log = new LogWriter(seed: 2);
        log.Startup();
        log.RoutineUntil(4_119);
        while (log.Count < 5_000)
        {
            log.Add(
                "ERR",
                "Orders.Api.Data.OrderRepository",
                "Failed to open a database connection. Npgsql.NpgsqlException (0x80004005): The connection pool has been exhausted, "
                + "either raise 'Max Pool Size' (currently 100) or 'Timeout' (currently 15 seconds) in your connection string.");
            if (log.Count < 5_000)
            {
                log.Add(
                    "WRN",
                    "Microsoft.AspNetCore.Hosting.Diagnostics",
                    $"Request finished HTTP/1.1 GET /orders/{log.Next(1_000, 9_999)} - 503 - application/problem+json {log.Next(15_000, 15_200)}ms");
            }
        }

        return log.Case(
            "connection-pool-exhaustion-is-reported",
            Findings([(4_120, 5_000)], categories: ["resource", "dependency"], severities: ["critical"], min: 1));
    }

    // 300 lines, ending with an unhandled exception, its stack trace and the host stopping.
    private static GoldenCase ProcessCrash()
    {
        var log = new LogWriter(seed: 3);
        log.Startup();
        log.RoutineUntil(295);
        log.Add("FTL", "Orders.Api.Program", "Unhandled exception. System.InvalidOperationException: Sequence contains no elements");
        log.AddRaw("   at System.Linq.ThrowHelper.ThrowNoElementsException()");
        log.AddRaw("   at Orders.Api.Tenants.TenantSettingsLoader.LoadAsync(CancellationToken cancellationToken) in /src/Orders.Api/Tenants/TenantSettingsLoader.cs:line 31");
        log.Add("INF", "Microsoft.Hosting.Lifetime", "Application is shutting down...");
        log.Add("INF", "Microsoft.Hosting.Lifetime", "Application stopped.");
        return log.Case("process-crash-is-critical", Findings([(296, 300)], severities: ["critical"], min: 1));
    }

    // 400 lines; slow queries roughly every 13 lines, each followed by its request succeeding slowly. Nothing fails.
    private static GoldenCase SlowQueriesWithoutFailures()
    {
        var log = new LogWriter(seed: 4);
        log.Startup();
        var slow = new List<(int From, int To)>();
        while (log.Count < 398)
        {
            log.RoutineUntil(Math.Min(log.Count + log.Next(10, 16), 396));
            var milliseconds = log.Next(2_100, 4_800);
            var query = log.Add(
                "WRN",
                "Microsoft.EntityFrameworkCore.Database.Command",
                $"Executed DbCommand ({milliseconds.ToString("N0", CultureInfo.InvariantCulture)}ms) [Parameters=[@p0='?'], "
                + "CommandType='Text', CommandTimeout='30'] SELECT o.id, o.total, o.created_at FROM orders AS o WHERE "
                + "o.customer_id = @p0 ORDER BY o.created_at DESC");
            var request = log.Add(
                "INF",
                "Microsoft.AspNetCore.Hosting.Diagnostics",
                $"Request finished HTTP/1.1 GET /customers/{log.Next(100, 999)}/orders - 200 - application/json {milliseconds + log.Next(5, 40)}ms");
            slow.Add((query, request));
        }

        log.RoutineUntil(400);
        return log.Case(
            "slow-queries-without-failures-are-a-warning",
            Findings(slow, severities: ["warning"], min: 1),
            Findings(slow, severities: ["critical", "error", "info"], max: 0));
    }

    // 700 lines with 200 identical timeouts calling inventory-service; every request still succeeds.
    private static GoldenCase RepeatedTimeout()
    {
        var log = new LogWriter(seed: 5);
        log.Startup();
        var timeouts = new List<(int From, int To)>();
        while (timeouts.Count < 200)
        {
            log.RoutineUntil(log.Count + log.Next(1, 3));
            var line = log.Add(
                "ERR",
                "Orders.Api.Inventory.InventoryClient",
                "Call to inventory-service timed out after 30000 ms: GET http://inventory-service/api/stock/SKU-1042");
            timeouts.Add((line, line));
        }

        log.RoutineUntil(700);
        return log.Case("repeated-timeout-becomes-one-finding", Findings(timeouts, min: 1, max: 1));
    }

    // 300 lines of startup messages, health checks and successful requests only.
    private static GoldenCase CleanLog()
    {
        var log = new LogWriter(seed: 6);
        log.Startup();
        log.RoutineUntil(300);
        return log.Case(
            "clean-log-has-no-findings",
            new Grader { Kind = GraderKinds.NoFindings },
            new Grader
            {
                Kind = GraderKinds.Summary,
                Question = "Does this summary say that no problems were found in the log?",
                Expected = "yes",
            });
    }

    private static Grader Findings(
        IReadOnlyList<(int From, int To)> lines,
        string[]? categories = null,
        string[]? severities = null,
        int? min = null,
        int? max = null) =>
        new()
        {
            Kind = GraderKinds.Findings,
            Lines = [.. lines.Select(range => new[] { range.From, range.To })],
            Categories = categories,
            Severities = severities,
            Min = min,
            Max = max,
        };

    /// <summary>Builds one log line by line, with times that move forward by a seeded amount.</summary>
    private sealed class LogWriter(int seed)
    {
        private static readonly string[] Paths = ["/orders", "/orders/{0}", "/customers/{0}", "/catalog/items/{0}"];

        private readonly Random _random = new(seed);

        private readonly StringBuilder _text = new();

        private DateTime _time = new(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc);

        /// <summary>How many lines are written; the next line is number <c>Count + 1</c>.</summary>
        public int Count { get; private set; }

        public int Next(int min, int max) => _random.Next(min, max + 1);

        /// <summary>Writes a line with a timestamp, a level and its source, and returns its number.</summary>
        public int Add(string level, string source, string message)
        {
            _time = _time.AddMilliseconds(_random.Next(40, 1_500));
            return AddRaw(string.Create(CultureInfo.InvariantCulture, $"{_time:yyyy-MM-dd HH:mm:ss.fff} +00:00 [{level}] {source}: {message}"));
        }

        public int AddRaw(string line)
        {
            _text.Append(line).Append('\n');
            return ++Count;
        }

        public void Startup()
        {
            Add("INF", "Microsoft.Hosting.Lifetime", "Now listening on: http://[::]:8080");
            Add("INF", "Microsoft.Hosting.Lifetime", "Application started. Press Ctrl+C to shut down.");
            Add("INF", "Microsoft.Hosting.Lifetime", "Hosting environment: Production");
            Add("INF", "Microsoft.Hosting.Lifetime", "Content root path: /app");
        }

        public void RoutineUntil(int lastLine)
        {
            while (Count < lastLine)
            {
                Routine();
            }
        }

        // A line of normal work: a successful request, a health check or an order stored.
        public void Routine()
        {
            switch (_random.Next(0, 10))
            {
                case < 2:
                    Add("INF", "Microsoft.AspNetCore.Hosting.Diagnostics", $"Request finished HTTP/1.1 GET /health - 200 - text/plain {Next(1, 3)}ms");
                    break;
                case < 4:
                    Add("INF", "Orders.Api.Orders.OrderService", $"Order {Next(10_000, 99_999)} stored for customer {Next(100, 999)}");
                    break;
                default:
                    var path = string.Format(CultureInfo.InvariantCulture, Paths[_random.Next(Paths.Length)], Next(1_000, 9_999));
                    Add("INF", "Microsoft.AspNetCore.Hosting.Diagnostics", $"Request finished HTTP/1.1 GET {path} - 200 - application/json {Next(4, 90)}ms");
                    break;
            }
        }

        public GoldenCase Case(string scenario, params Grader[] graders)
        {
            var logFile = $"{scenario}.log";
            return new GoldenCase(
                $"{Capability}/{scenario}",
                $"{Capability}/{logFile}",
                _text.ToString(),
                EvalCase.Serialize(new EvalCase { Log = logFile, Graders = [.. graders] }));
        }
    }
}

/// <summary>One generated case: its scenario id, its log's path below GoldenLogs/ and text, and its case file's text.</summary>
public sealed record GoldenCase(string Scenario, string LogPath, string Log, string CaseJson);
