﻿namespace Owin.Security.Providers.Imgur
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using Microsoft.Owin.Infrastructure;
    using Microsoft.Owin.Logging;
    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.Infrastructure;
    using Microsoft.Owin.Security.Provider;

    using Newtonsoft.Json;

    using Provider;

    /// <summary></summary>
    public class ImgurAuthenticationHandler : AuthenticationHandler<ImgurAuthenticationOptions>
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        /// <summary>Creates a new <see cref="ImgurAuthenticationHandler"/>.</summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> to be used for back channel calls.</param>
        /// <param name="logger">The <see cref="ILogger"/> to be used by the <see cref="ImgurAuthenticationHandler"/>.</param>
        public ImgurAuthenticationHandler(HttpClient httpClient, ILogger logger)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>Is called once by common code after initialization.</summary>
        /// <returns>Return true if the request is handled by this <see cref="AuthenticationMiddleware{TOptions}"/>, returns false if the request should be passed to the next <see cref="AuthenticationMiddleware{TOptions}"/>.</returns>
        public override async Task<bool> InvokeAsync()
        {
            if (!Options.CallbackPath.HasValue)
            {
                return false;
            }

            if (!Options.CallbackPath.Value.Equals(Request.Path.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var ticket = await AuthenticateAsync();

            if (ticket == null)
            {
                throw new Exception(ImgurAuthenticationDefaults.InvalidAuthenticationTicketMessage);
            }

            var context = GetImgurReturnEndpointContext(ticket);

            await Options.Provider.ReturnEndpoint(context);

            SignIn(context);

            if (context.IsRequestCompleted || context.RedirectUri == null)
            {
                return context.IsRequestCompleted;
            }

            var location = GetRedirectLocation(context);

            Response.Redirect(location);

            context.RequestCompleted();

            return context.IsRequestCompleted;
        }

        /// <summary>Handles authentication challenges by intercepting 401 responses.</summary>
        /// <returns>A <see cref="Task"/> representing the completed operation.</returns>
        protected override Task ApplyResponseChallengeAsync()
        {
            if (Response.StatusCode != 401)
            {
                return Task.FromResult<object>(null);
            }

            var challenge = Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode);

            if (challenge == null)
            {
                return Task.FromResult<object>(null);
            }

            if (string.IsNullOrWhiteSpace(challenge.Properties.RedirectUri))
            {
                challenge.Properties.RedirectUri = Request.Uri.AbsoluteUri;
            }

            GenerateCorrelationId(challenge.Properties);

            var state = Options.StateDataFormat.Protect(challenge.Properties);
            var authorizationUri = GetAuthorizationUri(state);

            Response.Redirect(authorizationUri);

            return Task.FromResult<object>(null);
        }

        /// <summary>The core authentication logic which must be provided by the <see cref="AuthenticationHandler{TOptions}"/>.</summary>
        /// <returns>The ticket data provided by the authentication logic.</returns>
        /// <remarks>Will be invoked at most once per request. Do not call directly, call the wrapping Authenticate method instead.</remarks>
        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            if (Request.Query.Get(ImgurAuthenticationDefaults.ErrorParameter) != null)
            {
                return new AuthenticationTicket(null, null);
            }

            var code = Request.Query.Get(ImgurAuthenticationDefaults.CodeParameter);
            var state = Request.Query.Get(ImgurAuthenticationDefaults.StateParameter);
            var properties = Options.StateDataFormat.Unprotect(state);

            if (properties == null)
            {
                return new AuthenticationTicket(null, null);
            }

            if (!ValidateCorrelationId(properties, _logger))
            {
                return new AuthenticationTicket(null, properties);
            }

            var authenticationResponse = await GetAuthenticationResponseAsync(code);

            if (authenticationResponse == null)
            {
                throw new Exception(ImgurAuthenticationDefaults.DeserializationFailureMessage);
            }

            var identity = GetIdentity(authenticationResponse);
            var context = GetImgurAuthenticatedContext(authenticationResponse, identity, properties);

            await Options.Provider.Authenticated(context);

            return new AuthenticationTicket(context.Identity, context.Properties);
        }

        /// <summary>Gets the payload for the back channel authentication request.</summary>
        /// <param name="code">The authorization code supplied by imgur.</param>
        /// <returns>The <see cref="HttpContent"/> with the payload for the back channel authentication request.</returns>
        private HttpContent GetAuthenticationRequestContent(string code)
        {
            return
                new FormUrlEncodedContent(
                    new[]
                    {
                        new KeyValuePair<string, string>(
                            ImgurAuthenticationDefaults.ClientIdParameter,
                            Options.ClientId),

                        new KeyValuePair<string, string>(
                            ImgurAuthenticationDefaults.ClientSecretParameter,
                            Options.ClientSecret),

                        new KeyValuePair<string, string>(
                            ImgurAuthenticationDefaults.GrantTypeParameter,
                            ImgurAuthenticationDefaults.AuthorizationCodeGrantType),

                        new KeyValuePair<string, string>(
                            ImgurAuthenticationDefaults.CodeParameter,
                            code)
                    });
        }

        /// <summary>Gets the <see cref="AuthenticationResponse"/> from imgur.</summary>
        /// <param name="code">The authorization code supplied by imgur.</param>
        /// <returns>The <see cref="AuthenticationResponse"/> from imgur.</returns>
        private async Task<AuthenticationResponse> GetAuthenticationResponseAsync(string code)
        {
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, ImgurAuthenticationDefaults.TokenUrl))
            {
                httpRequestMessage.Content = GetAuthenticationRequestContent(code);

                using (var httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage, Request.CallCancelled))
                {
                    if (!httpResponseMessage.IsSuccessStatusCode)
                    {
                        throw new Exception(ImgurAuthenticationDefaults.CommunicationFailureMessage);
                    }

                    using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                    {
                        var jsonSerializer = new JsonSerializer();

                        using (var streamReader = new StreamReader(stream))
                        {
                            using (var jsonTextReader = new JsonTextReader(streamReader))
                            {
                                return jsonSerializer.Deserialize<AuthenticationResponse>(jsonTextReader);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Gets the authorization URL for the back channel call.</summary>
        /// <param name="state">The encrypted <see cref="AuthenticationProperties"/> for the current authentication session.</param>
        /// <returns>The authorization URL for the back channel call.</returns>
        private string GetAuthorizationUri(string state)
        {
            var authorizationUri = ImgurAuthenticationDefaults.AuthorizationUrl;

            authorizationUri =
                WebUtilities.AddQueryString(
                    authorizationUri,
                    ImgurAuthenticationDefaults.ClientIdParameter,
                    Uri.EscapeDataString(Options.ClientId));

            authorizationUri =
                WebUtilities.AddQueryString(
                    authorizationUri,
                    ImgurAuthenticationDefaults.ResponseTypeParameter,
                    ImgurAuthenticationDefaults.CodeResponseType);

            authorizationUri =
                WebUtilities.AddQueryString(
                    authorizationUri,
                    ImgurAuthenticationDefaults.StateParameter,
                    Uri.EscapeDataString(state));

            return authorizationUri;
        }

        /// <summary>Gets the <see cref="ClaimsIdentity"/> for the identity of the user.</summary>
        /// <param name="authenticationResponse">The <see cref="AuthenticationResponse"/> returned by imgur.</param>
        /// <returns>The <see cref="ClaimsIdentity"/> for the identity of the user.</returns>
        private ClaimsIdentity GetIdentity(AuthenticationResponse authenticationResponse)
        {
            var identity =
                new ClaimsIdentity(
                    Options.AuthenticationType,
                    ClaimsIdentity.DefaultNameClaimType,
                    ClaimsIdentity.DefaultRoleClaimType);

            identity.AddClaim(
                new Claim(
                    ClaimTypes.Name,
                    authenticationResponse.AccountUsername,
                    ImgurAuthenticationDefaults.XmlSchemaString,
                    Options.AuthenticationType));

            identity.AddClaim(
                new Claim(
                    ClaimTypes.NameIdentifier,
                    authenticationResponse.AccountId.ToString(ImgurAuthenticationDefaults.Int32Format, CultureInfo.InvariantCulture),
                    ImgurAuthenticationDefaults.XmlSchemaString,
                    Options.AuthenticationType));

            identity.AddClaim(
                new Claim(
                    ClaimsIdentity.DefaultNameClaimType,
                    authenticationResponse.AccountUsername,
                    ImgurAuthenticationDefaults.XmlSchemaString,
                    Options.AuthenticationType));

            return identity;
        }

        /// <summary>Gets the <see cref="ImgurAuthenticatedContext"/> for the current authentication session.</summary>
        /// <param name="authenticationResponse">The <see cref="AuthenticationResponse"/> returned by imgur.</param>
        /// <param name="identity">The <see cref="ClaimsIdentity"/> for the identity of the user.</param>
        /// <param name="properties">The <see cref="AuthenticationProperties"/> for the current authentication session.</param>
        /// <returns>The <see cref="ImgurAuthenticatedContext"/> for the current authentication session.</returns>
        private ImgurAuthenticatedContext GetImgurAuthenticatedContext(AuthenticationResponse authenticationResponse, ClaimsIdentity identity, AuthenticationProperties properties)
        {
            var context = new ImgurAuthenticatedContext(Context, Options)
            {
                AccessToken = authenticationResponse.AccessToken,
                AccountId = authenticationResponse.AccountId,
                AccountUsername = authenticationResponse.AccountUsername,
                ExpiresIn = authenticationResponse.ExpiresIn,
                Identity = identity,
                Properties = properties,
                RefreshToken = authenticationResponse.RefreshToken,
                Scope = authenticationResponse.Scope,
                TokenType = authenticationResponse.TokenType
            };

            return context;
        }

        /// <summary>Gets the <see cref="ImgurReturnEndpointContext"/> for the current authentication session.</summary>
        /// <param name="ticket">The <see cref="AuthenticationTicket"/> for the current authentication session.</param>
        /// <returns>The <see cref="ImgurReturnEndpointContext"/> for the current authentication session.</returns>
        private ImgurReturnEndpointContext GetImgurReturnEndpointContext(AuthenticationTicket ticket)
        {
            var context = new ImgurReturnEndpointContext(Context, ticket)
            {
                SignInAsAuthenticationType = Options.SignInAsAuthenticationType,
                RedirectUri = ticket.Properties.RedirectUri
            };

            return context;
        }

        /// <summary>Adds authentication information to the OWIN context to let the appropriate <see cref="AuthenticationMiddleware{TOptions}"/> authenticate the user.</summary>
        /// <param name="context">The <see cref="ReturnEndpointContext"/> for the current authentication session.</param>
        private void SignIn(ReturnEndpointContext context)
        {
            if (context.SignInAsAuthenticationType == null || context.Identity == null)
            {
                return;
            }

            var identity = context.Identity;

            if (!identity.AuthenticationType.Equals(context.SignInAsAuthenticationType, StringComparison.OrdinalIgnoreCase))
            {
                identity =
                    new ClaimsIdentity(
                        identity.Claims,
                        context.SignInAsAuthenticationType,
                        identity.NameClaimType,
                        identity.RoleClaimType);
            }

            Context.Authentication.SignIn(context.Properties, identity);
        }

        /// <summary>Gets the URL where the user should be redirect to.</summary>
        /// <param name="context">The <see cref="ReturnEndpointContext"/> for the current authentication session.</param>
        /// <returns>The URL where the user should be redirect to.</returns>
        private static string GetRedirectLocation(ReturnEndpointContext context)
        {
            var location = context.RedirectUri;

            if (context.Identity == null)
            {
                location =
                    WebUtilities.AddQueryString(
                        location,
                        ImgurAuthenticationDefaults.ErrorParameter,
                        ImgurAuthenticationDefaults.AccessDeniedErrorMessage);
            }

            return location;
        }

        /// <summary>Response payload returned by imgur with the information of the authenticated user.</summary>
        private class AuthenticationResponse
        {
            /// <summary>Gets or sets the access token for the authenticated user.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.AccessTokenPropertyName)]
            public string AccessToken { get; set; }

            /// <summary>Gets or sets the account id of the authenticated user.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.AccountIdPropertyName)]
            public int AccountId { get; set; }

            /// <summary>Gets or sets the account username of the authenticated user.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.AccountUsernamePropertyName)]
            public string AccountUsername { get; set; }

            /// <summary>Gets or sets the duration of the access token.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.ExpiresInPropertyName)]
            public int ExpiresIn { get; set; }

            /// <summary>Gets or sets the refresh token for the authenticated user.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.RefreshInPropertyName)]
            public string RefreshToken { get; set; }

            /// <summary>Gets or sets the scope of the access token.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.ScopePropertyName)]
            public string Scope { get; set; }

            /// <summary>Gets or sets the type of the access token.</summary>
            [JsonProperty(PropertyName = ImgurAuthenticationDefaults.TokenTypePropertyName)]
            public string TokenType { get; set; }
        }
    }
}
