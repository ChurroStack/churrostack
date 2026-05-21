using DispatchR.Abstractions.Send;
using static ChurrOS.Api.Commands.ApiKeys.CreateApiKey;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class CreateApiKey : IRequest<CreateApiKey, ValueTask<CreateApiKeyResponse>>
    {
        public class CreateApiKeyResponse
        {
            public string Id { get; private set; }
            public string ApiKey { get; private set; }
            public DateTimeOffset ExpiresAt { get; private set; }

            public CreateApiKeyResponse(string id, string apiKey, DateTimeOffset expiresAt)
            {
                Id = id;
                ApiKey = apiKey;
                ExpiresAt = expiresAt;
            }
        }

        public class CreateApiKeyBody
        {
            public string? IdentityName { get; private set; }
            public string? Description { get; private set; }
            public DateTimeOffset? ExpiresAt { get; private set; }

            public CreateApiKeyBody(string? identityName, string? description, DateTimeOffset? expiresAt)
            {
                IdentityName = identityName;
                Description = description;
                ExpiresAt = expiresAt;
            }
        }

        public CreateApiKeyBody Body { get; private set; }

        public CreateApiKey(CreateApiKeyBody body)
        {
            Body = body;
        }
    }
}
