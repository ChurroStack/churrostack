using ChurrOS.Api.Models.Dtos;
using DispatchR.Abstractions.Send;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Commands.Git
{
    public class GetGitRepositoryInfo : IRequest<GetGitRepositoryInfo, ValueTask<GitRepositoryItem>>
    {
        public class GetGitRepositoryInfoBody
        {
            [Required]
            public string Environment { get; private set; }

            [Required]
            public string Url { get; private set; }

            public string? Username { get; private set; }
            public string? Password { get; private set; }

            public GetGitRepositoryInfoBody(string environment, string url, string? username, string? password)
            {
                Environment = environment;
                Url = url;
                Username = username;
                Password = password;
            }
        }

        public GetGitRepositoryInfoBody Body { get; private set; }

        public GetGitRepositoryInfo(GetGitRepositoryInfoBody body)
        {
            Body = body;
        }
    }
}
