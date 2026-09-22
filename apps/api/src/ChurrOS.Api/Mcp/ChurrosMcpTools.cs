using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Commands.Gallery;
using ChurrOS.Api.Commands.Llm;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ChurrOS.Api.Mcp
{
    // Reuses the exact DispatchR commands the REST controllers already use (GetEnvironments,
    // GetApplications, GetLlms, GetGalleryLlms), so authorization is identical to calling the API
    // directly: tenant resolution via MultiTenantMiddleware/ITenantResolver and the ACL/role checks
    // inside each handler apply unchanged. Nothing MCP-specific is re-implemented here.
    [McpServerToolType]
    public sealed class ChurrosMcpTools
    {
        private readonly IMediator _mediator;

        public ChurrosMcpTools(IMediator mediator)
        {
            _mediator = mediator;
        }

        [McpServerTool(Name = "list_environments")]
        [Description("List environments the caller can access. Search is a case-sensitive substring " +
            "match on the environment name. Tags require ALL listed tags to be present. Results are " +
            "paginated (default page size 25) but NOT ordered, so page boundaries are not stable " +
            "across separate calls.")]
        public async Task<QueryResult<EnvironmentSummary>> ListEnvironments(
            [Description("Case-sensitive substring match on the environment name.")] string? search = null,
            [Description("Environment must have ALL of these tags to match.")] string[]? tags = null,
            [Description("1-based page number. Default 1.")] int page = 1,
            [Description("Rows per page. Default 25; not validated or capped by the server.")] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            var query = new GetEnvironments.EnvironmentQueryRequest
            {
                Search = search,
                Tags = tags,
                Page = page,
                PageSize = pageSize,
            };

            return await _mediator.Send(new GetEnvironments(query), cancellationToken);
        }

        [McpServerTool(Name = "list_apps")]
        [Description("List applications the caller can access, optionally filtered to one " +
            "environment by name. The environment filter is an EXACT, case-sensitive match; an " +
            "unknown or unauthorized environment name returns an empty list (count: 0), not an " +
            "error. Search, tags, and pagination behave the same as list_environments.")]
        public async Task<QueryResult<ApplicationSummary>> ListApps(
            [Description("Exact, case-sensitive environment name to filter to. Omit to list across all accessible environments.")] string? environment = null,
            [Description("Case-sensitive substring match on the application name.")] string? search = null,
            [Description("\"application\" or \"workspace\".")] ApplicationMode? mode = null,
            [Description("Exact identity name of the application's creator.")] string? createdBy = null,
            [Description("Application must have ALL of these tags to match.")] string[]? tags = null,
            [Description("1-based page number. Default 1.")] int page = 1,
            [Description("Rows per page. Default 25; not validated or capped by the server.")] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            var query = new GetApplications.ApplicationQueryRequest
            {
                Environment = environment,
                Search = search,
                Mode = mode,
                CreatedBy = createdBy,
                Tags = tags,
                Page = page,
                PageSize = pageSize,
            };

            return await _mediator.Send(new GetApplications(query), cancellationToken);
        }

        [McpServerTool(Name = "list_llms")]
        [Description("List LLMs. scope=\"configured\" (default) lists LLM configurations you have " +
            "Read access to, with a richer shape (id, names, audit fields). " +
            "scope=\"executable\" lists only models you have Execute access to -- i.e. models you " +
            "can actually call right now -- with a lighter shape (icon, names).")]
        public async Task<object> ListLlms(
            [Description("\"configured\" (LLM configurations you can view) or \"executable\" (models you can actually call).")] string scope = "configured",
            [Description("Case-sensitive substring match on any of the LLM's names.")] string? search = null,
            [Description("1-based page number. Default 1.")] int page = 1,
            [Description("Rows per page. Default 25; not validated or capped by the server.")] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(scope, "executable", StringComparison.OrdinalIgnoreCase))
            {
                var galleryQuery = new QueryRequest { Search = search, Page = page, PageSize = pageSize };
                return await _mediator.Send(new GetGalleryLlms(galleryQuery), cancellationToken);
            }

            var query = new GetLlms.LlmsQueryRequest { Search = search, Page = page, PageSize = pageSize };
            return await _mediator.Send(new GetLlms(query), cancellationToken);
        }
    }
}
