namespace ChurrOS.Api.Models.Dtos.Environment
{
    public class EnvironmentKeysItem
    {
        public string SshPublicKey { get; protected set; }

        public string SshPrivateKey { get; protected set; }

        public string EncryptionKey { get; protected set; }

        public string Host { get; protected set; }

        public int Port { get; protected set; }

        public string KnownHosts { get; protected set; }

        public string Namespace { get; protected set; }

        public string valuesYaml { get; protected set; }

        public EnvironmentKeysItem(string sshPublicKey, string sshPrivateKey, string encryptionKey, string host, int port, string knownHosts, string @namespace, string valuesYaml)
        {
            SshPublicKey = sshPublicKey;
            SshPrivateKey = sshPrivateKey;
            EncryptionKey = encryptionKey;
            Host = host;
            Port = port;
            KnownHosts = knownHosts;
            Namespace = @namespace;
            this.valuesYaml = valuesYaml;
        }
    }
}
