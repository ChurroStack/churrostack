using ChurrOS.Api.Commands.Llm;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Utils;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using static ChurrOS.Api.Commands.Llm.GetLlms;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;

namespace ChurrOS.Api.Controllers
{
    [Authorize(Policy = "ApiKeyPolicy")]
    [Route("api/openai")]
    public class OpenAiController : ControllerBase
    {
        private static JsonSerializerOptions? _jsonSerializerOptions;
        private readonly IMediator _mediator;

        public OpenAiController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("models")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(QueryResult<LlmSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LlmSummary>> GetLlms([FromQuery] LlmsQueryRequest query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetLlms(query), cancellationToken);
            var models = new List<OaiModel>();
            foreach (var item in result.Items)
            {
                foreach (var name in item.Names)
                {
                    models.Add(new OaiModel(name, "model", item.CreatedAt.ToUnixTimeSeconds(), item.CreatedBy.Name));
                }
            }
            return Ok(new OaiModels("list", models));
        }

        [HttpPost("chat/completions")]
        [HttpPost("v1/chat/completions")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task CompletionWithLlms([FromBody] JsonElement body, CancellationToken cancellationToken)
        {
            var isStreaming = body.TryGetProperty("stream", out var jsonStream) && jsonStream.GetBoolean();
            if (isStreaming)
            {
                Response.Headers.ContentType = "text/event-stream";
                Response.Headers.CacheControl = "no-cache";
                Response.Headers.Connection = "keep-alive";
            }
            else
            {
                Response.Headers.ContentType = "application/json";
                Response.Headers.CacheControl = "no-cache";
            }

            string? xUserId = null;
            if (Request.Headers.TryGetValue("X-User-Id", out var headerValues))
            {
                xUserId = headerValues.FirstOrDefault();
            }

            await foreach (var update in _mediator.CreateStream(new CompletePromptWithLlm(body, isStreaming, isCompletion: true, xUserId), cancellationToken))
            {
                if (isStreaming)
                {
                    await SendUpdate(update, cancellationToken);
                }
                else
                {
                    Response.StatusCode = StatusCodes.Status200OK;
                    var json = JsonSerializer.Serialize(update);
                    await Response.WriteAsync(json, cancellationToken);
                    break;
                }
            }
            if (isStreaming)
            {
                await SendUpdate<object>(null, cancellationToken);
            }
        }


        [HttpPost("embeddings")]
        [HttpPost("v1/embeddings")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEmbeddingsWithLlm([FromBody] JsonElement body, CancellationToken cancellationToken)
        {
            string? xUserId = null;
            if (Request.Headers.TryGetValue("X-User-Id", out var headerValues))
            {
                xUserId = headerValues.FirstOrDefault();
            }

            var result = await _mediator.Send(new GetEmbeddingsWithLlm(body, xUserId), cancellationToken);
            return Ok(result);
        }

        [HttpPost("rerank")]
        [HttpPost("v1/rerank")]
        [HttpPost("v2/rerank")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorMessage), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RerankWithLlm([FromBody] JsonElement body, CancellationToken cancellationToken)
        {
            string? xUserId = null;
            if (Request.Headers.TryGetValue("X-User-Id", out var headerValues))
            {
                xUserId = headerValues.FirstOrDefault();
            }
            string path;
            var parts = HttpContext.Request.Path.Value?.Split('/');
            if (parts?.Length > 1 && parts[parts.Length - 2].StartsWith("v"))
            {
                path = $"{parts[parts.Length - 2]}/{parts[parts.Length - 1]}";
            }
            else
            {
                path = parts![parts.Length - 1];
            }

            var result = await _mediator.Send(new RerankWithLlm(body, path, xUserId), cancellationToken);
            return Ok(result);
        }

        private async Task SendUpdate<T>(T? @event, CancellationToken cancellationToken)
        {
            if (_jsonSerializerOptions is null)
            {
                _jsonSerializerOptions = new JsonSerializerOptions(JsonSettings.Value)
                {
                    WriteIndented = false
                };

                _jsonSerializerOptions.ApplyDefaultOptions();
            }

            if (@event is not null)
            {
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonSerializer.Serialize(@event, _jsonSerializerOptions).Trim('\r', '\n')}\r\n\r\n"), cancellationToken);
                //await Response.Body.FlushAsync(cancellationToken);
            }
            else
            {
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: [DONE]\r\n\r\n"), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
