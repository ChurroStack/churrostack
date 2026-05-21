using ChurrunKubernetes.Models.Dtos;
using ChurrunKubernetes.Models.Dtos.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using System.Net.Mime;

namespace ChurrunKubernetes.Middlewares
{
    public class ResponseExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.HttpContext?.Response?.HasStarted ?? false)
                return;

            object message = string.Empty;
            var exception = context.Exception;
            do
            {
                if (string.IsNullOrWhiteSpace((string)message))
                    message = exception.Message;
                else
                    message = $"{message} {exception.Message}";
                if (exception.InnerException is null)
                    break;
                exception = exception.InnerException;
            } while (exception != null);
            context.ExceptionHandled = true;

            if (context.Exception != null)
            {
                Console.WriteLine(context.Exception.ToString());
            }

            var content = new ErrorMessage(message?.ToString() ?? "Unknown error");

            switch (exception)
            {
                //case PostgresException pg:
                //    if (pg.SqlState == "23505")
                //    {
                //        context.Result = new ObjectResult(content)
                //        {
                //            StatusCode = (int?)HttpStatusCode.Conflict,
                //            ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection { MediaTypeNames.Application.Json }
                //        };
                //    }
                //    break;
                case HttpException ex:
                    if (ex.ResourceId is not null)
                        content.ResourceId = ex.ResourceId;
                    context.Result = new ObjectResult(content)
                    {
                        StatusCode = ex.Code,
                        ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection { MediaTypeNames.Application.Json }
                    };
                    break;
                case InvalidOperationException:
                    context.Result = new NotFoundObjectResult(content)
                    {
                        ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection { MediaTypeNames.Application.Json }
                    };
                    break;
                case ArgumentException:
                    context.Result = new BadRequestObjectResult(content)
                    {
                        ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection { MediaTypeNames.Application.Json }
                    };
                    break;
                case UnauthorizedAccessException:
                    context.Result = new UnauthorizedObjectResult(content)
                    {
                        StatusCode = (int?)HttpStatusCode.Forbidden,
                        ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection { MediaTypeNames.Application.Json }
                    };
                    break;
                default:
                    context.Result = new ObjectResult(content)
                    {
                        StatusCode = (int?)HttpStatusCode.InternalServerError,
                        ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection { MediaTypeNames.Application.Json }
                    };
                    break;
            }
        }
    }
}