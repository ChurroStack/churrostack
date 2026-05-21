using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace ChurrOS.Api.Commands.Git
{
    public class GetGitRepositoryInfoHandler : IRequestHandler<GetGitRepositoryInfo, ValueTask<GitRepositoryItem>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _dbContext;

        public GetGitRepositoryInfoHandler(IMediator mediator, ChurrosDbContext dbContext)
        {
            _mediator = mediator;
            _dbContext = dbContext;
        }

        public async ValueTask<GitRepositoryItem> Handle(GetGitRepositoryInfo request, CancellationToken cancellationToken)
        {
            var environmentRef = await _mediator.Send(new GetEnvironmentIdByName(request.Body.Environment), cancellationToken);

            var identityAcls = await _mediator.Send(new GetIdentityAcls(_dbContext.IdentityId, Permission.Execute), cancellationToken);

            if (!identityAcls.ContainsKey(environmentRef.AclId))
            {
                throw new UnauthorizedAccessException("You do not have permission to create new applications in this environment.");
            }

            var credentials = new CredentialsHandler(
                (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials()
                    {
                        Username = request.Body.Username ?? string.Empty,
                        Password = request.Body.Password ?? string.Empty
                    }
            );

            var branches = Repository.ListRemoteReferences(request.Body.Url, credentials)
                         .Where(elem => elem.IsLocalBranch)
                         .Select(elem => elem.CanonicalName
                         .Replace("refs/heads/", ""));

            return new GitRepositoryItem(request.Body.Url, branches.ToArray());
        }
    }
}
