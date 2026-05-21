using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using System.Net;
using System.Text;

namespace ChurrOS.Api.Services.Security
{
    public static class SshKeyGenerator
    {
        public static (string PemPrivateKey, byte[] PublicKey) GenerateSshKeyPair()
        {
            var generator = new Ed25519KeyPairGenerator();
            var parameters = new Ed25519KeyGenerationParameters(new SecureRandom());
            generator.Init(parameters);

            var keyPair = generator.GenerateKeyPair();
            var keyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);

            var ppk = new StringBuilder();
            ppk.AppendLine("-----BEGIN OPENSSH PRIVATE KEY-----");
            var bytes = OpenSshPrivateKeyUtilities.EncodePrivateKey(keyPair.Private);
            ppk.AppendLine(Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks));
            ppk.AppendLine("-----END OPENSSH PRIVATE KEY-----");

            var publicKeyParams = (Ed25519PublicKeyParameters)keyPair.Public;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            void WriteSshString(byte[] data)
            {
                bw.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length)));
                bw.Write(data);
            }
            var keyType = Encoding.ASCII.GetBytes("ssh-ed25519");
            WriteSshString(keyType);
            WriteSshString(publicKeyParams.GetEncoded());
            var publicKey = ms.ToArray();
            var base64Key = Convert.ToBase64String(publicKey);

            return (ppk.ToString(), publicKey);
        }

    }
}
