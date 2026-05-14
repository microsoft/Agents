// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public static class AspNetExtensions
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _openIdMetadataCache = new();

    /// <summary>
    /// Adds JWT bearer token validation for Azure Bot Service and agent-to-agent requests, reading settings from configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add authentication services to.</param>
    /// <param name="configuration">The application configuration containing a <see cref="TokenValidationOptions"/> section.</param>
    /// <param name="tokenValidationSectionName">
    /// Name of the configuration section to read <see cref="TokenValidationOptions"/> from.  Defaults to <c>"TokenValidation"</c>.
    /// </param>
    /// <remarks>
    /// <para>
    /// If the configuration section is absent or contains <c>"Enabled": false</c>, authentication is not configured and
    /// all requests will be treated as unauthenticated.  This is useful for local development only.
    /// </para>
    /// <para>
    /// Minimum configuration for Azure Public cloud:
    /// <code>
    /// "TokenValidation": {
    ///   "Audiences": [ "{{ClientId}}" ],
    ///   "TenantId": "{{TenantId}}"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Minimum configuration for Azure Government cloud — add <c>"IsGov": true</c>:
    /// <code>
    /// "TokenValidation": {
    ///   "Audiences": [ "{{ClientId}}" ],
    ///   "TenantId": "{{TenantId}}",
    ///   "IsGov": true
    /// }
    /// </code>
    /// Setting <c>IsGov</c> automatically selects the correct government-cloud issuer URLs and OpenID metadata
    /// endpoints.  See <see cref="TokenValidationOptions.IsGov"/> for the full list of defaults that are applied.
    /// </para>
    /// <para>
    /// For China or other sovereign clouds, omit <c>IsGov</c> and set
    /// <see cref="TokenValidationOptions.AzureBotServiceOpenIdMetadataUrl"/>,
    /// <see cref="TokenValidationOptions.OpenIdMetadataUrl"/>, and
    /// <see cref="TokenValidationOptions.ValidIssuers"/> explicitly.
    /// See <see cref="TokenValidationOptions"/> for the full set of available settings.
    /// </para>
    /// </remarks>
    public static void AddAgentAspNetAuthentication(this IServiceCollection services, IConfiguration configuration, string tokenValidationSectionName = "TokenValidation")
    {
        IConfigurationSection tokenValidationSection = configuration.GetSection(tokenValidationSectionName);

        if (!tokenValidationSection.Exists() || !tokenValidationSection.GetValue("Enabled", true))
        {
            // Noop if TokenValidation section missing or disabled.
            System.Diagnostics.Trace.WriteLine("AddAgentAspNetAuthentication: Auth disabled");
            return;
        }

        services.AddAgentAspNetAuthentication(tokenValidationSection.Get<TokenValidationOptions>()!);
    }

    /// <summary>
    /// Adds JWT bearer token validation for Azure Bot Service and agent-to-agent requests using the supplied options.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add authentication services to.</param>
    /// <param name="validationOptions">The fully populated <see cref="TokenValidationOptions"/> to use.</param>
    public static void AddAgentAspNetAuthentication(this IServiceCollection services, TokenValidationOptions validationOptions)
    {
        AssertionHelpers.ThrowIfNull(validationOptions, nameof(validationOptions));
        services.AddControllers();

        // Must have at least one Audience.
        if (validationOptions.Audiences == null || validationOptions.Audiences.Count == 0)
        {
            throw new ArgumentException($"{nameof(TokenValidationOptions)}:Audiences requires at least one ClientId");
        }

        // Audience values must be GUID's
        foreach (var audience in validationOptions.Audiences)
        {
            if (!Guid.TryParse(audience, out _))
            {
                throw new ArgumentException($"{nameof(TokenValidationOptions)}:Audiences values must be a GUID");
            }
        }

        // If ValidIssuers is empty, default for ABS Public Cloud
        if (validationOptions.ValidIssuers == null || validationOptions.ValidIssuers.Count == 0)
        {
            if (validationOptions.IsGov)
            {
                validationOptions.ValidIssuers =
                [
                    AuthenticationConstants.GovBotFrameworkTokenIssuer,
                    "https://sts.windows.net/cab8a31a-1906-4287-a0d8-4eef66b95f6e/",
                    "https://login.microsoftonline.us/cab8a31a-1906-4287-a0d8-4eef66b95f6e/v2.0"
                ];

                if (!string.IsNullOrEmpty(validationOptions.TenantId) && Guid.TryParse(validationOptions.TenantId, out _))
                {
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, validationOptions.TenantId));
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidGovernmentTokenIssuerUrlTemplateV2, validationOptions.TenantId));
                }
            }
            else
            {
                validationOptions.ValidIssuers =
                [
                    AuthenticationConstants.BotFrameworkTokenIssuer,
                    "https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/",
                    "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0",
                    "https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/",
                    "https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0",
                    "https://sts.windows.net/69e9b82d-4842-4902-8d1e-abc5b98a55e8/",
                    "https://login.microsoftonline.com/69e9b82d-4842-4902-8d1e-abc5b98a55e8/v2.0",
                ];

                if (!string.IsNullOrEmpty(validationOptions.TenantId) && Guid.TryParse(validationOptions.TenantId, out _))
                {
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, validationOptions.TenantId));
                    validationOptions.ValidIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, validationOptions.TenantId));
                }
            }
        }

        // If the `AzureBotServiceOpenIdMetadataUrl` setting is not specified, use the default based on `IsGov`.  This is what is used to authenticate ABS tokens.
        if (string.IsNullOrEmpty(validationOptions.AzureBotServiceOpenIdMetadataUrl))
        {
            validationOptions.AzureBotServiceOpenIdMetadataUrl = validationOptions.IsGov ? AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl : AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl;
        }

        // If the `OpenIdMetadataUrl` setting is not specified, use the default based on `IsGov`.  This is what is used to authenticate Entra ID tokens.
        if (string.IsNullOrEmpty(validationOptions.OpenIdMetadataUrl))
        {
            validationOptions.OpenIdMetadataUrl = validationOptions.IsGov ? AuthenticationConstants.GovOpenIdMetadataUrl : AuthenticationConstants.PublicOpenIdMetadataUrl;
        }

        var openIdMetadataRefresh = validationOptions.OpenIdMetadataRefresh ?? BaseConfigurationManager.DefaultAutomaticRefreshInterval;

        _ = services.AddAuthentication(options =>
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
                ValidIssuers = validationOptions.ValidIssuers,
                ValidAudiences = validationOptions.Audiences,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
            };

            // Using Microsoft.IdentityModel.Validators
            options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

            options.Events = new JwtBearerEvents
            {
                // Create a ConfigurationManager based on the requestor.  This is to handle ABS non-Entra tokens.
                OnMessageReceived = async context =>
                {
                    string authorizationHeader = context.Request.Headers.Authorization.ToString();

                    if (string.IsNullOrEmpty(authorizationHeader))
                    {
                        // Default to AadTokenValidation handling
                        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                        await Task.CompletedTask.ConfigureAwait(false);
                        return;
                    }

                    string[] parts = authorizationHeader?.Split(' ')!;
                    if (parts.Length != 2 || parts[0] != "Bearer")
                    {
                        // Default to AadTokenValidation handling
                        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                        await Task.CompletedTask.ConfigureAwait(false);
                        return;
                    }

                    JwtSecurityToken token = new(parts[1]);
                    string issuer = token.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.IssuerClaim)?.Value!;

                    if (validationOptions.AzureBotServiceTokenHandling 
                        && (AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase) 
                        || AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase)
                        || AuthenticationConstants.ChinaBotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Use the Azure Bot authority for this configuration manager
                        context.Options.TokenValidationParameters.ConfigurationManager = _openIdMetadataCache.GetOrAdd(validationOptions.AzureBotServiceOpenIdMetadataUrl, key =>
                        {
                            return new ConfigurationManager<OpenIdConnectConfiguration>(validationOptions.AzureBotServiceOpenIdMetadataUrl, new OpenIdConnectConfigurationRetriever(), new HttpClient())
                            {
                                AutomaticRefreshInterval = openIdMetadataRefresh
                            };
                        });
                    }
                    else
                    {
                        context.Options.TokenValidationParameters.ConfigurationManager = _openIdMetadataCache.GetOrAdd(validationOptions.OpenIdMetadataUrl, key =>
                        {
                            return new ConfigurationManager<OpenIdConnectConfiguration>(validationOptions.OpenIdMetadataUrl, new OpenIdConnectConfigurationRetriever(), new HttpClient())
                            {
                                AutomaticRefreshInterval = openIdMetadataRefresh
                            };
                        });
                    }

                    await Task.CompletedTask.ConfigureAwait(false);
                },

                OnTokenValidated = context =>
                {
                    return Task.CompletedTask;
                },
                OnForbidden = context =>
                {
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    return Task.CompletedTask;
                }
            };
        });
    }

    /// <summary>
    /// Settings that control JWT bearer token validation for Azure Bot Service and agent-to-agent requests.
    /// Read from the <c>TokenValidation</c> configuration section by <see cref="AddAgentAspNetAuthentication(IServiceCollection, IConfiguration, string)"/>.
    /// </summary>
    /// <remarks>
    /// An <c>Enabled</c> key may also appear in the same configuration section.  When set to <c>false</c>,
    /// authentication is disabled entirely and this class is not read.  This key is not a property of
    /// <see cref="TokenValidationOptions"/> because it is evaluated before deserialization.
    /// </remarks>
    public class TokenValidationOptions
    {
        /// <summary>
        /// One or more Client IDs of the Azure Bot registration.  At least one value is required.
        /// </summary>
        public IList<string>? Audiences { get; set; }

        /// <summary>
        /// Tenant ID of the Azure Bot.  Optional but recommended.
        /// When provided, tenant-specific issuer URLs are added to <see cref="ValidIssuers"/> automatically.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Override the list of trusted token issuers.  Optional.
        /// When omitted, default issuers are derived from <see cref="IsGov"/> and <see cref="TenantId"/>.
        /// For Public cloud the defaults include the Azure Bot Service issuer and common Microsoft tenant issuers.
        /// For Gov cloud the defaults include <see cref="AuthenticationConstants.GovBotFrameworkTokenIssuer"/> plus
        /// tenant-specific issuer URLs built from <see cref="AuthenticationConstants.ValidTokenIssuerUrlTemplateV1"/>
        /// and <see cref="AuthenticationConstants.ValidGovernmentTokenIssuerUrlTemplateV2"/>.
        /// For China or other clouds all issuers must be set explicitly since there is no corresponding <c>IsChina</c> flag.
        /// </summary>
        public IList<string>? ValidIssuers { get; set; }

        /// <summary>
        /// Set to <c>true</c> for Azure Government (USGov) cloud deployments.  Defaults to <c>false</c> (Public cloud).
        /// When <c>true</c>, the following defaults are applied to any property that is not set explicitly:
        /// <list type="bullet">
        /// <item><description>
        /// <see cref="AzureBotServiceOpenIdMetadataUrl"/> →
        /// <see cref="AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl"/>
        /// (<c>https://login.botframework.azure.us/v1/.well-known/openidconfiguration</c>)
        /// </description></item>
        /// <item><description>
        /// <see cref="OpenIdMetadataUrl"/> →
        /// <see cref="AuthenticationConstants.GovOpenIdMetadataUrl"/>
        /// (<c>https://login.microsoftonline.us/cab8a31a-1906-4287-a0d8-4eef66b95f6e/v2.0/.well-known/openid-configuration</c>)
        /// </description></item>
        /// <item><description>
        /// <see cref="ValidIssuers"/> →
        /// <see cref="AuthenticationConstants.GovBotFrameworkTokenIssuer"/> (<c>https://api.botframework.us</c>),
        /// plus tenant-specific v1 and v2 issuer URLs when <see cref="TenantId"/> is provided.
        /// </description></item>
        /// </list>
        /// For China or other sovereign clouds, leave this <c>false</c> and set all URLs and issuers explicitly.
        /// </summary>
        public bool IsGov { get; set; } = false;

        /// <summary>
        /// OpenID Connect metadata URL used to validate tokens issued by Azure Bot Service.  Optional.
        /// When omitted, defaults to <see cref="AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl"/> when
        /// <see cref="IsGov"/> is <c>false</c>, or <see cref="AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl"/>
        /// when <see cref="IsGov"/> is <c>true</c>.
        /// Set explicitly for China or other sovereign clouds.
        /// </summary>
        public string? AzureBotServiceOpenIdMetadataUrl { get; set; }

        /// <summary>
        /// OpenID Connect metadata URL used to validate Entra ID (AAD) tokens.  Optional.
        /// When omitted, defaults to <see cref="AuthenticationConstants.PublicOpenIdMetadataUrl"/> when
        /// <see cref="IsGov"/> is <c>false</c>, or <see cref="AuthenticationConstants.GovOpenIdMetadataUrl"/>
        /// when <see cref="IsGov"/> is <c>true</c>.
        /// Set explicitly for China or other sovereign clouds.
        /// </summary>
        public string? OpenIdMetadataUrl { get; set; }

        /// <summary>
        /// Enables special handling for tokens issued directly by Azure Bot Service (as opposed to Entra ID tokens).
        /// Defaults to <c>true</c> and should remain <c>true</c> until Azure Bot Service sends Entra ID tokens exclusively.
        /// When <c>true</c>, the <see cref="AzureBotServiceOpenIdMetadataUrl"/> endpoint is used for ABS token validation
        /// and <see cref="OpenIdMetadataUrl"/> is used for all other tokens.
        /// </summary>
        public bool AzureBotServiceTokenHandling { get; set; } = true;

        /// <summary>
        /// How frequently the OpenID Connect metadata is refreshed from the identity provider.  Defaults to 12 hours.
        /// </summary>
        public TimeSpan? OpenIdMetadataRefresh { get; set; }
    }
}
