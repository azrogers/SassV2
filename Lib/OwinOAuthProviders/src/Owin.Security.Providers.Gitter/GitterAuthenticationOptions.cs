﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataHandler;
using Owin.Security.Providers.Gitter.Provider;

namespace Owin.Security.Providers.Gitter
{
    public class GitterAuthenticationOptions : AuthenticationOptions
    {
        /// <summary>
        ///     Initializes a new <see cref="GitterAuthenticationOptions" />
        /// </summary>
        public GitterAuthenticationOptions()
            : base("Gitter")
        {
            Caption = Constants.DefaultAuthenticationType;
            AuthenticationMode = AuthenticationMode.Passive;
            CallbackPath = new PathString("/signin-gitter");
            BackchannelTimeout = TimeSpan.FromSeconds(60);
            Scope = new List<string>();
        }

        /// <summary>
        ///     Get or sets the text that the user can display on a sign in user interface.
        /// </summary>
        public string Caption
        {
            get { return Description.Caption; }
            set { Description.Caption = value; }
        }

        /// <summary>
        ///     Determines the permissions granted from the application to Gitter.
        ///     Options include: identify, read, post, client, and/or admin
        /// </summary>
        public IList<string> Scope { get; private set; }

        /// <summary>
        ///     Gets or sets the Google supplied Client ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        ///     Gets or sets the Google supplied Client Secret
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="IGitterAuthenticationProvider" /> used in the authentication events
        /// </summary>
        public IGitterAuthenticationProvider Provider { get; set; }

        /// <summary>
        ///     Gets or sets the type used to secure data handled by the middleware.
        /// </summary>
        public PropertiesDataFormat StateDataFormat { get; set; }

        /// <summary>
        ///     Gets or sets the name of another authentication middleware which will be responsible for actually issuing a user
        ///     <see cref="System.Security.Claims.ClaimsIdentity" />.
        /// </summary>
        public string SignInAsAuthenticationType { get; set; }

        /// <summary>
        ///     Gets or sets timeout value in milliseconds for back channel communications with Google+.
        /// </summary>
        /// <value>
        ///     The back channel timeout in milliseconds.
        /// </value>
        public TimeSpan BackchannelTimeout { get; set; }

        /// <summary>
        ///     The HttpMessageHandler used to communicate with Gitter.
        ///     This cannot be set at the same time as BackchannelCertificateValidator unless the value
        ///     can be downcast to a WebRequestHandler.
        /// </summary>
        public HttpMessageHandler BackchannelHttpHandler { get; set; }

        /// <summary>
        ///     Gets or sets the a pinned certificate validator to use to validate the endpoints used
        ///     in back channel communications belong to PayPal.
        /// </summary>
        /// <value>
        ///     The pinned certificate validator.
        /// </value>
        /// <remarks>
        ///     If this property is null then the default certificate checks are performed,
        ///     validating the subject name and if the signing chain is a trusted party.
        /// </remarks>
        public ICertificateValidator BackchannelCertificateValidator { get; set; }

        /// <summary>
        ///     The request path within the application's base path where the user-agent will be returned.
        ///     The middleware will process this request when it arrives.
        ///     Default value is "/signin-gitter".
        /// </summary>
        public PathString CallbackPath { get; set; }

    }
}