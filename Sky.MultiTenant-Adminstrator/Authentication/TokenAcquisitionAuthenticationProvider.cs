using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Cosmos.MultiTenant.Administrator.Authentication
{
    /// <summary>
    /// Custom authentication provider that uses ITokenAcquisition to get tokens.
    /// </summary>
    public class TokenAcquisitionAuthenticationProvider : IAuthenticationProvider
    {
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly string[]? _scopes;

        /// <summary>
        /// Initializes a new instance of the TokenAcquisitionAuthenticationProvider class.
        /// </summary>
        /// <param name="tokenAcquisition">The token acquisition service.</param>
        /// <param name="scopes">The scopes to request.</param>
        public TokenAcquisitionAuthenticationProvider(ITokenAcquisition tokenAcquisition, string[]? scopes = null)
        {
            _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
            _scopes = scopes ?? new[] { "https://graph.microsoft.com/.default" };
        }

        /// <summary>
        /// Authenticates the request by adding a bearer token.
        /// </summary>
        /// <param name="request">The request information to authenticate.</param>
        /// <param name="additionalAuthenticationContext">Optional additional context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var token = await _tokenAcquisition.GetAccessTokenForUserAsync(_scopes);
            request.Headers.Add("Authorization", $"Bearer {token}");
        }
    }
}
