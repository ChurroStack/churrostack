using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Applications
{
    public class UpdateApplication : IRequest<UpdateApplication, ValueTask<ApplicationItem>>
    {
        public string Name { get; private set; }
        public JsonElement Body { get; private set; }

        public UpdateApplication(string name, JsonElement body)
        {
            Name = name;
            Body = body;
        }
    }
}
