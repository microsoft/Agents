// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;

public static class AspNetExtensions
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _openIdMetadataCache = new();

    private static bool IsBotFrameworkIssuer(string issuer)
    {
        return AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase)
            || AuthenticationConstants.GovBotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase)
            || AuthenticationConstants.ChinaBotFrameworkTokenIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds JWT bearer token validation for Azure Bot Service and agent-to-agent requests.
    /// This overload is designed for use with <c>AddAgentAuthorization</c>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="tokenValidationSectionName">
    /// Name of the configuration section to read <see cref="AspNetExtensions.TokenValidationOptions"/> from.  Defaults to <c>"TokenValidation"</c>.
    /// </param>
    public static void AddAgentAspNetAuthentication(this IHostApplicationBuilder builder, string tokenValidationSectionName = "TokenValidation")
    {
        builder.Services.AddAgentAspNetAuthentication(builder.Configuration, tokenValidationSectionName);
    }

    /// <summary>
    /// Adds JWT bearer token validation for Azure Bot Service and agent-to-agent requests, reading settings from configuration.
    /// </summary>
    /// <param name="services">The <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> to add authentication services to.</param>
    /// <param name="configuration">The application configuration containing a <see cref="AspNetExtensions.TokenValidationOptions"/> section.</param>
    /// <param name="tokenValidationSectionName">
    /// Name of the configuration section to read <see cref="AspNetExtensions.TokenValidationOptions"/> from.  Defaults to <c>"TokenValidation"</c>.
    /// </param>
    /// <remarks>
    /// <para>
    /// If the configuration section is absent, an <see cref="System.ArgumentException"/> is thrown.
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
    /// endpoints.  See <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> for the full list of defaults that are applied.
    /// </para>
    /// <para>
    /// For China or other sovereign clouds, omit <c>IsGov</c> and set
    /// <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceOpenIdMetadataUrl"/>,
    /// <see cref="AspNetExtensions.TokenValidationOptions.OpenIdMetadataUrl"/>, and
    /// <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/> explicitly.
    /// See <see cref="AspNetExtensions.TokenValidationOptions"/> for the full set of available settings.
    /// </para>
    /// </remarks>
    public static void AddAgentAspNetAuthentication(this IServiceCollection services, IConfiguration configuration, string tokenValidationSectionName = "TokenValidation")
    {
        IConfigurationSection tokenValidationSection = configuration.GetSection(tokenValidationSectionName);

        if (!tokenValidationSection.Exists())
        {
            throw new ArgumentException($"Configuration section '{tokenValidationSectionName}' is missing. Token validation requires a valid configuration section.");
        }

        services.AddAgentAspNetAuthentication(tokenValidationSection.Get<TokenValidationOptions>()!);
    }

    /// <summary>
    /// Adds JWT bearer token validation for Azure Bot Service and agent-to-agent requests using the supplied options.
    /// </summary>
    /// <param name="services">The <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> to add authentication services to.</param>
    /// <param name="validationOptions">The fully populated <see cref="AspNetExtensions.TokenValidationOptions"/> to use.</param>
    public static void AddAgentAspNetAuthentication(this IServiceCollection services, TokenValidationOptions validationOptions)
    {
        AssertionHelpers.ThrowIfNull(validationOptions, nameof(validationOptions));

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
            if (validationOptions.AzureBotServiceOnly)
            {
                // Accept only Azure Bot Service (BotFramework) tokens.  Entra ID / agent-to-agent
                // callers are rejected at issuer validation (ValidateIssuer) before reaching the
                // AllowedCallers check.  Requires AzureBotServiceTokenHandling to remain true so
                // these tokens are validated against the ABS OpenID metadata.
                validationOptions.ValidIssuers =
                [
                    validationOptions.IsGov
                        ? AuthenticationConstants.GovBotFrameworkTokenIssuer
                        : AuthenticationConstants.BotFrameworkTokenIssuer
                ];
            }
            else if (validationOptions.IsGov)
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

        if (validationOptions.AllowDynamicTenantIssuers)
        {
            if (validationOptions.AllowedCallers == null
                || validationOptions.AllowedCallers.Count == 0
                || validationOptions.AllowedCallers.Any(c => string.IsNullOrWhiteSpace(c) || c.Equals("*", StringComparison.Ordinal)))
            {
                throw new ArgumentException($"{nameof(TokenValidationOptions)}:{nameof(TokenValidationOptions.AllowedCallers)} must contain specific caller App IDs when dynamic tenant issuers are enabled.");
            }

            if (validationOptions.AzureBotServiceOnly)
            {
                throw new ArgumentException($"{nameof(TokenValidationOptions)}:{nameof(TokenValidationOptions.AzureBotServiceOnly)} cannot be combined with {nameof(TokenValidationOptions.AllowDynamicTenantIssuers)}.");
            }

            var expectedHost = validationOptions.IsGov ? "login.microsoftonline.us" : "login.microsoftonline.com";
            if (!Uri.TryCreate(validationOptions.OpenIdMetadataUrl, UriKind.Absolute, out var metadataUrl)
                || metadataUrl.Scheme != Uri.UriSchemeHttps
                || !string.Equals(metadataUrl.Host, expectedHost, StringComparison.OrdinalIgnoreCase)
                || !metadataUrl.IsDefaultPort
                || !string.IsNullOrEmpty(metadataUrl.UserInfo)
                || !string.IsNullOrEmpty(metadataUrl.Query)
                || !string.IsNullOrEmpty(metadataUrl.Fragment))
            {
                throw new ArgumentException($"{nameof(TokenValidationOptions)}:{nameof(TokenValidationOptions.OpenIdMetadataUrl)} must use the configured Entra cloud when dynamic tenant issuers are enabled.");
            }
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

            if (validationOptions.AllowDynamicTenantIssuers)
            {
                options.TokenValidationParameters.IssuerValidator = (issuer, token, parameters) =>
                    ValidateDynamicIssuer(issuer, token, parameters, validationOptions);
            }

            // Using Microsoft.IdentityModel.Validators
            options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

            options.Events = new JwtBearerEvents
            {
                // Create a ConfigurationManager based on the requestor.  This is to handle ABS non-Entra tokens.
                OnMessageReceived = context =>
                {
                    string authorizationHeader = context.Request.Headers.Authorization.ToString();

                    if (string.IsNullOrEmpty(authorizationHeader))
                    {
                        // Default to AadTokenValidation handling
                        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                        return Task.CompletedTask;
                    }

                    string[] parts = authorizationHeader?.Split(' ')!;
                    if (parts.Length != 2 || parts[0] != "Bearer")
                    {
                        // Default to AadTokenValidation handling
                        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                        return Task.CompletedTask;
                    }

                    // Use JsonWebToken for lightweight issuer extraction without full token parsing
                    JsonWebToken token = new(parts[1]);
                    string issuer = token.Issuer;

                    if (validationOptions.AzureBotServiceTokenHandling
                        && IsBotFrameworkIssuer(issuer))
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

                    return Task.CompletedTask;
                },

                OnTokenValidated = context =>
                {
                    // AllowedCallers check for non-BotFramework tokens.
                    // BotFramework tokens (from ABS) are excluded since they use service-level issuers.
                    var issuer = context.Principal?.FindFirst("iss")?.Value;
                    bool isBotFrameworkToken = validationOptions.AzureBotServiceTokenHandling
                        && issuer != null && IsBotFrameworkIssuer(issuer);

                    if (!isBotFrameworkToken
                        && context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity
                        && !identity.IsTenantIdIssuerValid())
                    {
                        context.Fail("Token tenant ID does not match its issuer.");
                        return Task.CompletedTask;
                    }

                    if (!isBotFrameworkToken
                        && validationOptions.AllowedCallers != null
                        && validationOptions.AllowedCallers.Count > 0
                        && !validationOptions.AllowedCallers.Any(c => c.Equals("*", StringComparison.Ordinal)))
                    {
                        // azp (v2 tokens) or appid (v1 tokens)
                        var callerAppId = context.Principal?.FindFirst("azp")?.Value
                            ?? context.Principal?.FindFirst("appid")?.Value;

                        if (string.IsNullOrEmpty(callerAppId) || !validationOptions.AllowedCallers.Any(c => c.Equals(callerAppId, StringComparison.OrdinalIgnoreCase)))
                        {
                            context.Fail($"Caller App ID '{callerAppId}' is not in the AllowedCallers list.");
                        }
                    }

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

    private static string ValidateDynamicIssuer(string issuer, SecurityToken token, TokenValidationParameters parameters, TokenValidationOptions options)
    {
        if (string.IsNullOrEmpty(issuer) || token == null)
        {
            throw new SecurityTokenInvalidIssuerException("Token issuer is missing or invalid.");
        }

        if (IsBotFrameworkIssuer(issuer))
        {
            // Defaults are cloud-specific; explicit entries in ValidIssuers retain their existing trust override.
            if (options.AzureBotServiceTokenHandling
                && parameters.ValidIssuers?.Contains(issuer) == true)
            {
                return issuer;
            }

            throw new SecurityTokenInvalidIssuerException("Azure Bot Service issuer is not enabled or allowed.");
        }

        var isConfiguredIssuer = parameters.ValidIssuers?.Contains(issuer) == true;
        if (isConfiguredIssuer)
        {
            // Explicitly trusted issuers retain the same allow-list and post-validation checks as strict mode.
            return issuer;
        }

        if (IsCanonicalEntraIssuer(issuer, options.IsGov))
        {
            // The shared claim helper permits missing tenant IDs and unrecognized issuers for the
            // existing flow. Dynamic issuers must first have one raw tid and a canonical cloud URL.
            var claims = token switch
            {
                JsonWebToken jsonWebToken => jsonWebToken.Claims.ToList(),
                System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtToken => jwtToken.Claims.ToList(),
                _ => []
            };
            var tenants = claims.Where(claim => claim.Type == AuthenticationConstants.TenantIdClaim).ToList();

            if (tenants.Count != 1
                || !Guid.TryParseExact(tenants[0].Value, "D", out var tokenTenant)
                || tokenTenant == Guid.Empty)
            {
                throw new SecurityTokenInvalidIssuerException("Token tenant ID does not match its issuer.");
            }

            var identity = new ClaimsIdentity([new Claim(AuthenticationConstants.IssuerClaim, issuer)]);
            identity.AddClaims(claims.Where(claim => claim.Type != AuthenticationConstants.IssuerClaim));
            if (!identity.IsTenantIdIssuerValid())
            {
                throw new SecurityTokenInvalidIssuerException("Token tenant ID does not match its issuer.");
            }

            return issuer;
        }

        throw new SecurityTokenInvalidIssuerException("Token issuer is not an allowed canonical Entra issuer.");
    }

    private static bool IsCanonicalEntraIssuer(string issuer, bool isGov)
    {
        const string v1Prefix = "https://sts.windows.net/";
        var v2Prefix = isGov ? "https://login.microsoftonline.us/" : "https://login.microsoftonline.com/";
        const string v2Suffix = "/v2.0";

        string? tenantText = null;
        if (issuer.StartsWith(v1Prefix, StringComparison.Ordinal) && issuer.EndsWith("/", StringComparison.Ordinal))
        {
            tenantText = issuer.Substring(v1Prefix.Length, issuer.Length - v1Prefix.Length - 1);
        }
        else if (issuer.StartsWith(v2Prefix, StringComparison.Ordinal) && issuer.EndsWith(v2Suffix, StringComparison.Ordinal))
        {
            tenantText = issuer.Substring(v2Prefix.Length, issuer.Length - v2Prefix.Length - v2Suffix.Length);
        }

        return tenantText?.Length == 36
            && Guid.TryParseExact(tenantText, "D", out var tenantId)
            && tenantId != Guid.Empty;
    }

    /// <summary>
    /// Settings that control JWT bearer token validation for Azure Bot Service and agent-to-agent requests.
    /// Read from the <c>TokenValidation</c> configuration section by <see cref="AspNetExtensions.AddAgentAspNetAuthentication(Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, System.String)"/>.
    /// </summary>
    public class TokenValidationOptions
    {
        /// <summary>
        /// One or more Client IDs of the Azure Bot registration.  At least one value is required.
        /// </summary>
        public IList<string>? Audiences { get; set; }

        /// <summary>
        /// Tenant ID of the Azure Bot.  Optional but recommended.
        /// When provided, tenant-specific issuer URLs are added to <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/> automatically.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Override the list of trusted token issuers.  Optional.
        /// When omitted, default issuers are derived from <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> and <see cref="AspNetExtensions.TokenValidationOptions.TenantId"/>.
        /// For Public cloud the defaults include the Azure Bot Service issuer and common Microsoft tenant issuers.
        /// For Gov cloud the defaults include <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovBotFrameworkTokenIssuer"/> plus
        /// tenant-specific issuer URLs built from <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.ValidTokenIssuerUrlTemplateV1"/>
        /// and <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.ValidGovernmentTokenIssuerUrlTemplateV2"/>.
        /// For China or other clouds all issuers must be set explicitly since there is no corresponding <c>IsChina</c> flag.
        /// See also <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceOnly"/> to default this to just the BotFramework issuer.
        /// </summary>
        public IList<string>? ValidIssuers { get; set; }

        /// <summary>
        /// Allows canonical Entra issuers from runtime-selected tenants when their tenant GUID matches the
        /// token's <c>tid</c> claim. Disabled by default; issuer validation stays enabled in both modes.
        /// Supported only for public and US Government Entra metadata authorities. Configure
        /// <see cref="AspNetExtensions.TokenValidationOptions.AllowedCallers"/> with specific App IDs; unrestricted
        /// callers and <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceOnly"/> are rejected at startup.
        /// Enforce required roles or scopes when accepting callers from multiple tenants.
        /// </summary>
        public bool AllowDynamicTenantIssuers { get; set; } = false;

        /// <summary>
        /// Set to <c>true</c> for Azure Government (USGov) cloud deployments.  Defaults to <c>false</c> (Public cloud).
        /// When <c>true</c>, the following defaults are applied to any property that is not set explicitly:
        /// <list type="bullet">
        /// <item><description>
        /// <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceOpenIdMetadataUrl"/> →
        /// <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl"/>
        /// (<c>https://login.botframework.azure.us/v1/.well-known/openidconfiguration</c>)
        /// </description></item>
        /// <item><description>
        /// <see cref="AspNetExtensions.TokenValidationOptions.OpenIdMetadataUrl"/> →
        /// <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovOpenIdMetadataUrl"/>
        /// (<c>https://login.microsoftonline.us/cab8a31a-1906-4287-a0d8-4eef66b95f6e/v2.0/.well-known/openid-configuration</c>)
        /// </description></item>
        /// <item><description>
        /// <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/> →
        /// <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovBotFrameworkTokenIssuer"/> (<c>https://api.botframework.us</c>),
        /// plus tenant-specific v1 and v2 issuer URLs when <see cref="AspNetExtensions.TokenValidationOptions.TenantId"/> is provided.
        /// </description></item>
        /// </list>
        /// For China or other sovereign clouds, leave this <c>false</c> and set all URLs and issuers explicitly.
        /// </summary>
        public bool IsGov { get; set; } = false;

        /// <summary>
        /// Restrict the agent to accept only Azure Bot Service (BotFramework) traffic.  Defaults to <c>false</c>.
        /// When <c>true</c> and <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/> is not set explicitly, <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/> is
        /// defaulted to just the BotFramework token issuer
        /// (<see cref="Microsoft.Agents.Authentication.AuthenticationConstants.BotFrameworkTokenIssuer"/>, or
        /// <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovBotFrameworkTokenIssuer"/> when <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> is <c>true</c>).
        /// Entra ID and agent-to-agent callers are then rejected at issuer validation, so the
        /// <see cref="AspNetExtensions.TokenValidationOptions.AllowedCallers"/> check does not apply.
        /// <para>
        /// Note: this targets the legacy BotFramework issuer (<c>https://api.botframework.com</c>).  As Azure Bot
        /// Service migrates channels to send Entra ID tokens (see <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceTokenHandling"/>), those
        /// tokens are issued by the Bot Service Entra tenants rather than the BotFramework issuer; if you rely on
        /// that path, set <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/> explicitly instead.  Keep <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceTokenHandling"/>
        /// <c>true</c> when using this option.  For China or other sovereign clouds, set <see cref="AspNetExtensions.TokenValidationOptions.ValidIssuers"/>
        /// explicitly since there is no corresponding flag.
        /// </para>
        /// </summary>
        public bool AzureBotServiceOnly { get; set; } = false;

        /// <summary>
        /// OpenID Connect metadata URL used to validate tokens issued by Azure Bot Service.  Optional.
        /// When omitted, defaults to <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl"/> when
        /// <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> is <c>false</c>, or <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl"/>
        /// when <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> is <c>true</c>.
        /// Set explicitly for China or other sovereign clouds.
        /// </summary>
        public string? AzureBotServiceOpenIdMetadataUrl { get; set; }

        /// <summary>
        /// OpenID Connect metadata URL used to validate Entra ID (AAD) tokens.  Optional.
        /// When omitted, defaults to <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.PublicOpenIdMetadataUrl"/> when
        /// <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> is <c>false</c>, or <see cref="Microsoft.Agents.Authentication.AuthenticationConstants.GovOpenIdMetadataUrl"/>
        /// when <see cref="AspNetExtensions.TokenValidationOptions.IsGov"/> is <c>true</c>.
        /// Set explicitly for China or other sovereign clouds.
        /// </summary>
        public string? OpenIdMetadataUrl { get; set; }

        /// <summary>
        /// Enables special handling for tokens issued directly by Azure Bot Service (as opposed to Entra ID tokens).
        /// Defaults to <c>true</c> and should remain <c>true</c> until Azure Bot Service sends Entra ID tokens exclusively.
        /// When <c>true</c>, the <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceOpenIdMetadataUrl"/> endpoint is used for ABS token validation
        /// and <see cref="AspNetExtensions.TokenValidationOptions.OpenIdMetadataUrl"/> is used for all other tokens.
        /// </summary>
        public bool AzureBotServiceTokenHandling { get; set; } = true;

        /// <summary>
        /// How frequently the OpenID Connect metadata is refreshed from the identity provider.  Defaults to 12 hours.
        /// </summary>
        public TimeSpan? OpenIdMetadataRefresh { get; set; }

        /// <summary>
        /// List of Application IDs (Client IDs) that are allowed to call this agent.  Optional.
        /// When empty or containing <c>"*"</c>, any caller is accepted.
        /// When populated with specific App IDs, the <c>azp</c> or <c>appid</c> claim in the inbound token
        /// must match one of the listed values.  This check applies only to non-BotFramework tokens.
        /// To accept only Azure Bot Service (BotFramework) traffic, use <see cref="AspNetExtensions.TokenValidationOptions.AzureBotServiceOnly"/>.
        /// </summary>
        public IList<string>? AllowedCallers { get; set; }
    }
}
