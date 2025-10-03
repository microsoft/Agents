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

    public static void AddAgentAspNetAuthentication(this IServiceCollection services, IConfiguration configuration, string tokenValidationSectionName = "TokenValidation")
    {
        IConfigurationSection tokenValidationSection = configuration.GetSection(tokenValidationSectionName);

        if (!tokenValidationSection.Exists() || !tokenValidationSection.GetValue("Enabled", true))
        {
            System.Diagnostics.Trace.WriteLine("AddAgentAspNetAuthentication: Auth disabled");
            return;
        }

        services.AddAgentAspNetAuthentication(tokenValidationSection.Get<TokenValidationOptions>()!);
    }

    public static void AddAgentAspNetAuthentication(this IServiceCollection services, TokenValidationOptions validationOptions)
    {
        AssertionHelpers.ThrowIfNull(validationOptions, nameof(validationOptions));

        if (validationOptions.Audiences == null || validationOptions.Audiences.Count == 0)
        {
            throw new ArgumentException($"{nameof(TokenValidationOptions)}:Audiences requires at least one ClientId");
        }

        foreach (var audience in validationOptions.Audiences)
        {
            if (!Guid.TryParse(audience, out _))
            {
                throw new ArgumentException($"{nameof(TokenValidationOptions)}:Audiences values must be a GUID");
            }
        }

        if (validationOptions.ValidIssuers == null || validationOptions.ValidIssuers.Count == 0)
        {
            validationOptions.ValidIssuers =
            [
                "https://api.botframework.com",
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

        if (string.IsNullOrEmpty(validationOptions.AzureBotServiceOpenIdMetadataUrl))
        {
            validationOptions.AzureBotServiceOpenIdMetadataUrl = validationOptions.IsGov ? AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl : AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl;
        }

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

            options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = async context =>
                {
                    string authorizationHeader = context.Request.Headers.Authorization.ToString();

                    if (string.IsNullOrEmpty(authorizationHeader))
                    {
                        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                        await Task.CompletedTask.ConfigureAwait(false);
                        return;
                    }

                    string[] parts = authorizationHeader?.Split(' ')!;
                    if (parts.Length != 2 || parts[0] != "Bearer")
                    {
                        context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                        await Task.CompletedTask.ConfigureAwait(false);
                        return;
                    }

                    JwtSecurityToken token = new(parts[1]);
                    string issuer = token.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.IssuerClaim)?.Value!;

                    if (validationOptions.AzureBotServiceTokenHandling && AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer))
                    {
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
                OnTokenValidated = context => Task.CompletedTask,
                OnForbidden = context => Task.CompletedTask,
                OnAuthenticationFailed = context => Task.CompletedTask
            };
        });
    }

    public class TokenValidationOptions
    {
        public IList<string>? Audiences { get; set; }
        public string? TenantId { get; set; }
        public IList<string>? ValidIssuers { get; set; }
        public bool IsGov { get; set; } = false;
        public string? AzureBotServiceOpenIdMetadataUrl { get; set; }
        public string? OpenIdMetadataUrl { get; set; }
        public bool AzureBotServiceTokenHandling { get; set; } = true;
        public TimeSpan? OpenIdMetadataRefresh { get; set; }
    }
}
