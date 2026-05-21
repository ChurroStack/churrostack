namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationIdentifier
    {
        public long Id { get; protected set; }
        public long AclId { get; protected set; }
        public long EnvironmentAclId { get; protected set; }

        public ApplicationIdentifier(long id, long aclId, long environmentAclId)
        {
            Id = id;
            AclId = aclId;
            EnvironmentAclId = environmentAclId;
        }
    }
}
