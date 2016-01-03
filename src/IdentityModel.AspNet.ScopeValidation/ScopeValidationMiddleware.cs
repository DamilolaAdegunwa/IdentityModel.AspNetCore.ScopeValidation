﻿using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IdentityModel.AspNet.ScopeValidation
{
    /// <summary>
    /// Middleware to check for scope claims in principal
    /// </summary>
    public class ScopeValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ScopeValidationOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeValidationMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next midleware.</param>
        /// <param name="scopes">The scopes.</param>
        public ScopeValidationMiddleware(RequestDelegate next, ScopeValidationOptions options)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.ScopeClaimType))
            {
                throw new ArgumentNullException(nameof(options.ScopeClaimType));
            }

            if (options.AllowedScopes == null)
            {
                throw new ArgumentNullException(nameof(options.AllowedScopes));
            }

            _next = next;
            _options = options;
        }

        public async Task Invoke(HttpContext context)
        {
            ClaimsPrincipal principal;

            if (!string.IsNullOrWhiteSpace(_options.AuthenticationScheme))
            {
                principal = await context.Authentication.AuthenticateAsync(_options.AuthenticationScheme);
            }
            else
            {
                principal = context.User;
            }

            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                await _next(context);
                return;
            }

            if (ScopesFound(principal))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = 403;
            context.Response.Headers.Add("WWW-Authenticate", new[] { "Bearer error=\"insufficient_scope\"" });

            EmitCorsResponseHeaders(context);
        }

        private bool ScopesFound(ClaimsPrincipal principal)
        {
            var scopeClaims = principal.FindAll(_options.ScopeClaimType);

            if (scopeClaims == null || !scopeClaims.Any())
            {
                return false;
            }

            foreach (var scope in scopeClaims)
            {
                if (_options.AllowedScopes.Contains(scope.Value, StringComparer.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void EmitCorsResponseHeaders(HttpContext context)
        {
            StringValues values;

            if (context.Request.Headers.TryGetValue("Origin", out values))
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", values);
                context.Response.Headers.Add("Access-Control-Expose-Headers", new string[] { "WWW-Authenticate" });
            }

            if (context.Request.Headers.TryGetValue("Access-Control-Request-Method", out values))
            {
                context.Response.Headers.Add("Access-Control-Allow-Method", values);
            }

            if (context.Request.Headers.TryGetValue("Access-Control-Request-Headers", out values))
            {
                context.Response.Headers.Add("Access-Control-Allow-Headers", values);
            }
        }
    }
}