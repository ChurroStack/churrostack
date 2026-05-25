using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class RetypeCpuGpuMetricsAsGauge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data fix for the metrics-calculation postmortem (2026-05-22).
            // cpu_usage / gpu_usage were ingested as counters but are gauges, and the
            // persisted Metric.Type plus the historical samples were both wrong. A
            // scoped UPDATE + DELETE on cs.metric_value (a Timescale hypertable) takes
            // long enough on real data to block boot, so wipe both tables wholesale —
            // ScrapeMetricsJob recreates the series with the correct type on the next
            // scrape cycle.
            migrationBuilder.Sql("TRUNCATE TABLE cs.metric, cs.metric_value RESTART IDENTITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TRUNCATE is unrecoverable; nothing to revert.
        }
    }
}
