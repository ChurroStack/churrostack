using IdGen;
using System.Net;

namespace ChurrunKubernetes.Services
{
    public class IdGenerationService
    {
        private readonly IdGenerator _idGeneratorService;

        public IdGenerationService()
        {
            int.TryParse(Dns.GetHostName().Split('-').Last(), out var replicaId);
            _idGeneratorService = new IdGenerator(replicaId);
        }

        public long CreateLongId()
        {
            return _idGeneratorService.CreateId();
        }
    }
}
