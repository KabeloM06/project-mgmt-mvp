using Azure.Security.KeyVault.Secrets;

namespace backend.Services
{
    public class JwtConfig
    {
        private readonly SecretClient _secretClient;

        public JwtConfig(SecretClient secretClient)
        {
            _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        }

        /// <summary>
        /// Retrieves the JWT signing key dynamically from the zero-trust Azure Key Vault container.
        /// </summary>
        public string GetSigningKey()
        {
            // Fetches the secret cleanly in memory without ever caching it to local disk files
            KeyVaultSecret secret = _secretClient.GetSecret("JwtSigningKey");
            return secret.Value;
        }
    }
}