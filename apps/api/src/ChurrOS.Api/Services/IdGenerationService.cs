using IdGen;
using System.Net;
using UUIDNext;

namespace ChurrOS.Api.Services
{
    public class IdGenerationService : IIdGeneratorService
    {
        private readonly IdGenerator _idGeneratorService;

        public IdGenerationService()
        {
            int.TryParse(Dns.GetHostName().Split('-').Last(), out var replicaId);
            _idGeneratorService = new IdGenerator(replicaId);
        }

        public Guid CreateGuidId()
        {
            return Uuid.NewDatabaseFriendly(Database.PostgreSql);
        }

        public long CreateLongId()
        {
            return _idGeneratorService.CreateId();
        }
    }
}
