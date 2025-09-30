using Client.Utility;
using Spectre.Console;

namespace Client.Benchmark;

internal class ScreenView(BenchmarkTui tui)
{
    public Panel Render()
    {
        return new Panel(BuildGrid())
            .Header("Bachelor Akka Client", Justify.Center)
            .Border(BoxBorder.Rounded);
    }
    
    private Grid BuildGrid()
    {
        var g = new Grid().AddColumn().AddColumn();
        g.AddRow(ThroughputPanel(), LatencyPanel());
        g.AddRow(ClusterPanel(), ShowRaceSessions());
        g.AddRow(ControlsPanel());
        return g;
    }

    private Panel ThroughputPanel()
    {
        var m = MetricsSnapshot.Current;
        var table = new Table().NoBorder().AddColumn("").AddColumn("");
        table.AddRow("Current", Markup.Escape($"{m.Tps,8:F1} msg/s"));
        table.AddRow("Messages", Markup.Escape(m.MessagesTotal.ToString("N0")));
        table.AddRow("Running", Markup.Escape(m.TimeRunning.ToString("hh\\:mm\\:ss")));
        table.AddRow("Errors", Markup.Escape($"{m.ErrorsPct:F2}%"));


        var spark = new BreakdownChart()
            .AddItem("p50", m.P50, Color.Black)
            .AddItem("p95", m.P95, Color.Black)
            .AddItem("p99", m.P99, Color.Black);


        var layout = new Rows(table, spark);
        return new Panel(layout).Header("Throughput", Justify.Left);
    }

    private Panel LatencyPanel()
    {
        var m = MetricsSnapshot.Current;
        var t = new Table().NoBorder().AddColumn("").AddColumn("");
        t.AddRow("p50", $"{m.P50:F1} ms");
        t.AddRow("p95", $"{m.P95:F1} ms");
        t.AddRow("p99", $"{m.P99:F1} ms");
        t.AddRow("Min/Max", $"{m.Min:F1} / {m.Max:F1} ms");
        return new Panel(t).Header("Latency (ms)", Justify.Left);
    }

    private Panel ClusterPanel()
    {
        var m = MetricsSnapshot.Current;
        var t = new Table().NoBorder().AddColumn("Metric").AddColumn("Value");
        t.AddRow("Nodes", m.Nodes.ToString());
        t.AddRow("Shards", m.Shards.ToString());
        t.AddRow("Entities", m.Entities.ToString("N0"));
        if (m.ShardDist.Count > 0)
        {
            var dist = string.Join(" ", m.ShardDist.Select(kv => $"[blue]{kv.Key}[/]:{kv.Value}"));
            t.AddRow("Distribution", dist);
        }
        return new Panel(t).Header("Cluster & Shards", Justify.Left);
    }

    private Panel ShowRaceSessions()
    {
        var sessions = tui.Sessions;

        var t = new Table().NoBorder().HideHeaders();


        t.AddColumn(new TableColumn("").LeftAligned());
        t.AddColumn(new TableColumn("").LeftAligned());

        if (sessions is null)
        {
            t.AddRow("Location:", "N/A");
            t.AddRow("Country:", "N/A");
        }
        else
        {
            var first = sessions.First(x => x.selected).race;
            t.AddRow("Location:", first.CircuitName ?? "N/A");
            t.AddRow("Country:", first.CountryName ?? "N/A");
        }

        return new Panel(t).Header("Race", Justify.Left);
    }


    private Panel ControlsPanel()
    {
        var grid = new Grid().AddColumn().AddColumn().AddColumn().AddColumn();
        grid.AddRow(
            "[[F5]] Connection", 
            "[[F6]] Measure [[\u2191|\u2193]] Change Race",
            "[[S]] Start"
        );
        return new Panel(grid).Header("Controls", Justify.Left);
    }
}