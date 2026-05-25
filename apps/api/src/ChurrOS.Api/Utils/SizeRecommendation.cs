using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Environment;

namespace ChurrOS.Api.Utils
{
    /// <summary>
    /// Helpers for translating observed CPU/memory usage into an Application Size
    /// recommendation chosen from an environment's available sizes.
    /// </summary>
    public static class SizeRecommendation
    {
        // Direction values surfaced to the UI.
        public const string Downsize = "downsize";
        public const string Upsize = "upsize";
        public const string Resize = "resize";
        public const string Optimal = "optimal";
        public const string InsufficientData = "insufficient_data";
        public const string NotAnalyzed = "not_analyzed";

        /// <summary>Headroom applied to the CPU P95 when sizing.</summary>
        public const double CpuHeadroom = 1.3;

        /// <summary>Headroom applied to the memory peak when sizing.</summary>
        public const double MemoryHeadroom = 1.15;

        /// <summary>Minimum number of samples required before a recommendation is made.</summary>
        public const int MinSampleCount = 30;

        /// <summary>
        /// Picks the smallest environment size whose limits cover the required CPU
        /// (cores) and memory (bytes), staying within the same GPU tier as the current
        /// size. Falls back to the largest size when nothing is big enough, and returns
        /// null when the environment exposes no usable sizes.
        /// </summary>
        public static SizeRequestItem? PickSize(
            EnvironmentSizeDefinition[]? sizes,
            SizeRequestItem? currentSize,
            double requiredCpuCores,
            double requiredMemoryBytes)
        {
            if (sizes == null || sizes.Length == 0)
                return null;

            var currentGpu = NormalizeGpu(currentSize?.Gpu);

            var candidates = sizes
                .Select(size => new { Size = size, Limits = size.Limits ?? size.Requests })
                .Where(c => c.Limits != null)
                .Select(c => new
                {
                    c.Size,
                    Cpu = ParseCpu(c.Limits!.Cpu),
                    Memory = ParseMemory(c.Limits!.Memory),
                    Gpu = NormalizeGpu(c.Limits!.Gpu),
                })
                .Where(c => c.Cpu.HasValue && c.Memory.HasValue && c.Gpu == currentGpu)
                .OrderBy(c => c.Cpu!.Value)
                .ThenBy(c => c.Memory!.Value)
                .ToList();

            if (candidates.Count == 0)
                return null;

            var chosen = candidates.FirstOrDefault(c =>
                c.Cpu!.Value >= requiredCpuCores && c.Memory!.Value >= requiredMemoryBytes)
                ?? candidates[^1];

            return ToSizeRequestItem(chosen.Size);
        }

        /// <summary>
        /// Classifies a recommendation relative to the current size.
        /// </summary>
        public static string GetDirection(SizeRequestItem? currentSize, SizeRequestItem? recommendedSize)
        {
            if (recommendedSize == null)
                return InsufficientData;

            if (SameSize(currentSize, recommendedSize))
                return Optimal;

            var currentCpu = ParseCpu(currentSize?.Cpu) ?? 0;
            var recommendedCpu = ParseCpu(recommendedSize.Cpu) ?? 0;
            var currentMemory = ParseMemory(currentSize?.Memory) ?? 0;
            var recommendedMemory = ParseMemory(recommendedSize.Memory) ?? 0;

            var smaller = recommendedCpu <= currentCpu && recommendedMemory <= currentMemory;
            var larger = recommendedCpu >= currentCpu && recommendedMemory >= currentMemory;

            if (smaller && larger) return Optimal; // identical limits, only the name differs
            if (smaller) return Downsize;
            if (larger) return Upsize;
            return Resize; // mixed (e.g. less CPU but more memory)
        }

        /// <summary>
        /// Resolves the named preset that an app's size corresponds to, mirroring the UI
        /// size-picker fallback: prefer an explicit <see cref="SizeRequestItem.Hint"/>, else
        /// match by the (cpu, memory, gpu, storage) tuple against the environment's catalog.
        /// Returns null when no preset matches.
        /// </summary>
        public static string? ResolveHint(EnvironmentSizeDefinition[]? sizes, SizeRequestItem? size)
        {
            if (size == null)
                return null;
            if (!string.IsNullOrEmpty(size.Hint))
                return size.Hint;
            if (sizes == null || sizes.Length == 0)
                return null;

            var cpu = ParseCpu(size.Cpu);
            var memory = ParseMemory(size.Memory);
            var gpu = NormalizeGpu(size.Gpu);
            var storage = ParseMemory(size.Storage);

            foreach (var def in sizes)
            {
                var quota = def.Limits ?? def.Requests;
                if (quota == null)
                    continue;
                if (ParseCpu(quota.Cpu) != cpu) continue;
                if (ParseMemory(quota.Memory) != memory) continue;
                if (NormalizeGpu(quota.Gpu) != gpu) continue;
                // Storage is optional: when the app doesn't pin a storage value, accept
                // any preset (presets historically omit storage and adding it later
                // would silently lose every existing app's hint).
                if (storage != null && ParseMemory(quota.Storage) != storage) continue;
                return def.Name;
            }
            return null;
        }

        /// <summary>True when two sizes describe the same amount of resources.</summary>
        public static bool SameSize(SizeRequestItem? a, SizeRequestItem? b)
        {
            if (a == null || b == null)
                return false;

            if (!string.IsNullOrEmpty(a.Hint) && !string.IsNullOrEmpty(b.Hint))
                return string.Equals(a.Hint, b.Hint, StringComparison.Ordinal);

            return ParseCpu(a.Cpu) == ParseCpu(b.Cpu)
                && ParseMemory(a.Memory) == ParseMemory(b.Memory)
                && NormalizeGpu(a.Gpu) == NormalizeGpu(b.Gpu);
        }

        private static SizeRequestItem ToSizeRequestItem(EnvironmentSizeDefinition size)
        {
            var quota = size.Limits ?? size.Requests;
            return new SizeRequestItem(size.Name, quota?.Cpu, quota?.Memory, quota?.Storage, quota?.Gpu);
        }

        private static double? ParseCpu(string? cpu)
            => !string.IsNullOrWhiteSpace(cpu) && cpu.TryParseCpuToCores(out var cores) ? cores : null;

        private static double? ParseMemory(string? memory)
            => !string.IsNullOrWhiteSpace(memory) && memory.TryParseMemoryToBytes(out var bytes) ? bytes : null;

        private static double NormalizeGpu(string? gpu)
            => !string.IsNullOrWhiteSpace(gpu) && gpu.TryParseCpuToCores(out var count) ? count : 0;
    }
}
