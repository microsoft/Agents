// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Agents.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;


namespace DispatcherAgent
{
    public static class SampleServiceCollectionExtensions
    {
        /// <summary>
        /// Adds default token validation typical for ABS/SMBA.  If config settings are not supplied, this will
        /// default to Azure Public Cloud.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="rootConnectionConfig"></param>
        /// <remarks>
        /// Example config:
        ///     {
        ///        "TokenValidation": {
        ///           "ValidIssuers": [
        ///              "{default:Public-Azure}"
        ///           ],
        ///           "AllowedCallers": [
        ///              "{default:*}"
        ///           ]
        ///        }
        ///     }
        /// </remarks>
        public static void AddBotAspNetAuthentication(this IServiceCollection services, IConfiguration configuration, string botConnectionConfig = "BotServiceConnection")
        {
            var tokenValidationSection = configuration.GetSection("TokenValidation");

            var validTokenIssuers = tokenValidationSection.GetSection("ValidIssuers").Get<List<string>>();

            // If ValidIssuers is empty, default for ABS Public Cloud
            if (validTokenIssuers == null || validTokenIssuers.Count == 0)
            {
                validTokenIssuers =
                [
                    "https://api.botframework.com",
                    "https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/",
                    "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0",
                    "https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/",
                    "https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0",
                ];

                var tenantId = configuration[$"{botConnectionConfig}:TenantId"];
                if (!string.IsNullOrEmpty(tenantId))
                {
                    validTokenIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, tenantId));
                    validTokenIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, tenantId));
                }
            }

            //services.AddAuthentication().AddMicrosoftIdentityWebApi(configuration, "SiteIdentity")
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidIssuers = validTokenIssuers,
                    ValidAudience = configuration[$"Connections:{botConnectionConfig}:Settings:ClientId"],
                    RequireSignedTokens = true,
                    SignatureValidator = (token, parameters) => new JwtSecurityToken(token),
                };

                // The following lines Azure AD signing key issuer validation.
                //options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();
                //options.Events = new JwtBearerEvents
                //{
                //    OnMessageReceived = async context =>
                //    {
                //        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                //        await Task.CompletedTask.ConfigureAwait(false);
                //    }
                //};
            })
            //.AddMicrosoftIdentityWebApi(configuration, "SiteIdentity", "IgnoreMe")
            //.EnableTokenAcquisitionToCallDownstreamApi()
            ////.AddDownstreamApi("graph", configuration.GetSection("graphDownStream"))
            //.AddInMemoryTokenCaches()
            ; 

            ;
        }
    }
}
