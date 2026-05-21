namespace ChurrOS.Api.Services
{
    public interface IIdGeneratorService
    {
        public long CreateLongId();
        public Guid CreateGuidId();
    }
}
