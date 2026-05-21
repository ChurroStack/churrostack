using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentitiesHandler : IRequestHandler<GetIdentities, ValueTask<QueryResult<IdentityItem>>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;

        public GetIdentitiesHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IMediator mediator)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<IdentityItem>> Handle(GetIdentities request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.User, _dbContext.IdentityId), cancellationToken);

            var result = new List<IdentityItem>();
            var totalItems = 0;
            var identityQuery = _dbContext.Set<Domain.Identity>().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                identityQuery = identityQuery.Where(o => o.Name.ToLower().Contains(request.Search.ToLower()) || o.DisplayName.ToLower().Contains(request.Search.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                var types = request.Type.Split(',', ';');
                var typesParsed = new List<IdentityType>();
                foreach (var type in types)
                {
                    if (Enum.TryParse<IdentityType>(type, ignoreCase: true, out var typeParsed))
                    {
                        typesParsed.Add(typeParsed);
                    }
                }

                identityQuery = identityQuery.Where(o => typesParsed.Contains(o.Type));
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var roles = request.Role.Split(',', ';');
                var rolesParsed = new List<IdentityRole>();
                foreach (var rol in roles)
                {
                    if (Enum.TryParse<IdentityRole>(rol, ignoreCase: true, out var roleParsed))
                    {
                        rolesParsed.Add(roleParsed);
                    }
                }

                identityQuery = identityQuery.Where(o => rolesParsed.Contains(o.Role));
            }

            totalItems = await identityQuery.CountAsync(cancellationToken: cancellationToken);

            result = await identityQuery
                .Skip((int)request.Skip)
                .Take((int)request.Top)
                .Select(o => new IdentityItem(o.Id, o.Name, o.DisplayName, o.Role, o.Type, o.ModifiedAt))
                .ToListAsync(cancellationToken: cancellationToken);

            //if (request.IncludeNames?.Length > 0)
            //{
            //    var includes = await _dbContext.Set<Domain.Identity>()
            //                             .AsNoTracking()
            //                             .Where(o => request.IncludeNames.Contains(o.Name))
            //                             .Select(o => new IdentityItem(o.Name, o.DisplayName, o.Role, o.Type))
            //                             .ToListAsync(cancellationToken: cancellationToken);

            //    result = result.Concat(includes).ToList();
            //}

            return new QueryResult<IdentityItem>(result, totalItems);
        }
    }
}
