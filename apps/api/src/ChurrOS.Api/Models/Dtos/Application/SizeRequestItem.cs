namespace ChurrOS.Api.Models.Dtos.Application
{
    public class SizeRequestItem
    {
        public string? Hint { get; set; }
        public string? Cpu { get; set; }
        public string? Memory { get; set; }
        public string? Storage { get; set; }
        public string? Gpu { get; set; }

        public SizeRequestItem(string? hint, string? cpu, string? memory, string? storage, string? gpu)
        {
            Hint = hint;
            Cpu = cpu;
            Memory = memory;
            Storage = storage;
            Gpu = gpu;
        }
    }
}
