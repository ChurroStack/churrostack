namespace ChurrOS.Api.Models.Dtos.Environment
{
    public class EnvironmentIdentifier
    {
        public long Id { get; protected set; }
        public long AclId { get; protected set; }
        public string Type { get; protected set; }

        public EnvironmentIdentifier(long id, long aclId, string type)
        {
            Id = id;
            AclId = aclId;
            Type = type;
        }
    }
}
