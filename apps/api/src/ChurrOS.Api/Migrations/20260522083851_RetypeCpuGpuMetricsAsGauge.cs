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
            // cpu_usage / gpu_usage are instantaneous runner readings (gauges), but earlier
            // code ingested them as counters. Metric.Type is persisted per series, so existing
            // rows must be retyped or GetMetrics keeps applying Rate() to gauge samples.
            // MetricType enum: Counter = 0, Gauge = 1.
            migrationBuilder.Sql(
                "UPDATE cs.metric SET type = 1 " +
                "WHERE labels->>'metric' IN ('cpu_usage', 'gpu_usage') AND type = 0;");

            // Historical samples for these series hold ACCUMULATED counter values; read as a
            // gauge they render a monotonically rising ramp. Purge them so the charts show
            // only correct instantaneous samples collected after this migration.
            migrationBuilder.Sql(
                "DELETE FROM cs.metric_value WHERE metric_id IN (" +
                "SELECT metric_id FROM cs.metric WHERE labels->>'metric' IN ('cpu_usage', 'gpu_usage'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort revert of the type change. The purged metric_value history
            // cannot be restored.
            migrationBuilder.Sql(
                "UPDATE cs.metric SET type = 0 " +
                "WHERE labels->>'metric' IN ('cpu_usage', 'gpu_usage') AND type = 1;");
        }
    }
}
