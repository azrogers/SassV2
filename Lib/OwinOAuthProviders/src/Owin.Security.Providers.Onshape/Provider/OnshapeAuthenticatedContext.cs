// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Security.Claims;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Provider;
using Newtonsoft.Json.Linq;

namespace Owin.Security.Providers.Onshape
{
    /// <summary>
    /// Contains information about the login session as well as the user <see cref="System.Security.Claims.ClaimsIdentity"/>.
    /// </summary>
    public class OnshapeAuthenticatedContext : BaseContext
    {
        /// <summary>
        /// Initializes a <see cref="OnshapeAuthenticatedContext"/>
        /// </summary>
        /// <param name="context">The OWIN environment</param>
        /// <param name="user">The JSON-serialized user</param>
        /// <param name="accessToken">Onshape Access token</param>
        public OnshapeAuthenticatedContext(IOwinContext context, JObject user, string accessToken)
            : base(context)
        {
            AccessToken = accessToken;
            User = user;

            Id = TryGetValue(user, "id");
            Name = TryGetValue(user, "name");
        }

        /// <summary>
        /// Gets the JSON-serialized user
        /// </summary>
        /// <remarks>
        /// Contains the Onshape user obtained from the endpoint https://api.Onshape.com/1/account/info
        /// </remarks>
        public JObject User { get; private set; }

        /// <summary>
        /// Gets the Onshape OAuth access token
        /// </summary>
        public string AccessToken { get; private set; }

        /// <summary>
        /// Gets the Onshape user ID
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// The name of the user
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the <see cref="ClaimsIdentity"/> representing the user
        /// </summary>
        public ClaimsIdentity Identity { get; set; }

        /// <summary>
        /// Gets or sets a property bag for common authentication properties
        /// </summary>
        public AuthenticationProperties Properties { get; set; }

        private static string TryGetValue(JObject user, string propertyName)
        {
            JToken value;
            return user.TryGetValue(propertyName, out value) ? value.ToString() : null;
        }
    }
}
