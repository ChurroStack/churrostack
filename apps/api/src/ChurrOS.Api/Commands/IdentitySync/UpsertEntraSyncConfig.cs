using ChurrOS.Api.Models.Dtos.IdentitySync;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.IdentitySync
{
    public class UpsertEntraSyncConfig : IRequest<UpsertEntraSyncConfig, ValueTask<EntraSyncConfigItem>>
    {
        public EntraSyncConfig Body { get; protected set; }

        public UpsertEntraSyncConfig(EntraSyncConfig body)
        {
            Body = body;
        }
    }
}
