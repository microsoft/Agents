import "@typespec/http";
import "@typespec/openapi";
import "@typespec/openapi3";
import "./activity.tsp";

using Http;
using OpenAPI;
using ActivityProtocol;

/**
 * The Token Service API provides OAuth token management for agents.
 * It enables agents to acquire, exchange, and manage user OAuth tokens
 * for connected services (e.g., Microsoft Graph, third-party APIs).
 *
 * This API is served by the Azure Bot Service Token Service and is
 * consumed by agents via the Microsoft.Agents.Connector SDK.
 *
 * © 2026 Microsoft
 */
@service(#{ title: "TokenService" })
@info(#{
  version: "3.1.12",
  contact: #{ name: "Agents SDK", url: "https://github.com/microsoft/agents" },
  license: #{ name: "The MIT License (MIT)", url: "https://opensource.org/licenses/MIT" },
})
namespace TokenService;

// ============================================================================
// Token Service Models
// ============================================================================

/**
 * The status of a particular token for a user/connection.
 */
model TokenStatus {
  /** The channelId the token status pertains to. */
  channelId?: string;

  /** The name of the connection the token status pertains to. */
  connectionName?: string;

  /** True if a token is stored for this ConnectionName. */
  hasToken?: boolean;

  /** The display name of the service provider for which this token belongs to. */
  serviceProviderDisplayName?: string;
}

/**
 * A request to exchange a token (URI or existing token) for an OAuth token.
 */
model TokenExchangeRequest {
  /** The application ID / resource identifier with which to exchange a token on behalf of. */
  uri?: string;

  /** The token to exchange. */
  token?: string;
}

/**
 * A list of AAD resource URLs for which to retrieve tokens.
 */
model AadResourceUrls {
  /** The resource URLs to get tokens for. */
  resourceUrls?: string[];
}

/**
 * A type containing information for single sign-on.
 */
model SignInResource {
  /** The sign-in link. */
  signInLink?: string;

  /** Additional properties for token exchange operations. */
  tokenExchangeResource?: TokenExchangeResource;

  /** Additional properties for direct token posting operations. */
  tokenPostResource?: TokenPostResource;
}

/**
 * Response containing either a TokenResponse or a SignInResource.
 * When a cached or valid token is available, tokenResponse is populated.
 * Otherwise, signInResource provides the sign-in flow information.
 */
model TokenOrSignInResourceResponse {
  /** The token response, if a token is available. */
  tokenResponse?: TokenResponse;

  /** The sign-in resource, if sign-in is required. */
  signInResource?: SignInResource;
}

// ============================================================================
// User Token Operations
// ============================================================================

@route("api/usertoken")
interface UserToken {
  /**
   * Get a user token for a given connection.
   * Returns null (404) if the user has not yet authorized or the code is invalid.
   */
  @get
  @route("GetToken")
  getToken(
    @query userId: string,
    @query connectionName: string,
    @query channelId?: string,
    @query code?: string,
  ): TokenResponse | void;

  /**
   * Sign the user out of a connection, revoking the stored token.
   * Returns 200 with a body or 204 with no content.
   */
  @delete
  @route("SignOut")
  signOut(
    @query userId: string,
    @query connectionName?: string,
    @query channelId?: string,
  ): {} | void;

  /**
   * Get the token status for each configured connection for a user.
   */
  @get
  @route("GetTokenStatus")
  getTokenStatus(
    @query userId: string,
    @query channelId?: string,
    /** Filter to include only specific connections. */
    @query include?: string,
  ): TokenStatus[];

  /**
   * Get AAD tokens for the specified resource URLs.
   * Returns a map of resource URL to TokenResponse.
   */
  @post
  @route("GetAadTokens")
  getAadTokens(
    @query userId: string,
    @query connectionName: string,
    @query channelId?: string,
    @body body: AadResourceUrls,
  ): Record<TokenResponse>;

  /**
   * Exchange a token (URI-based or token-based) for an OAuth token.
   * Returns 400 if consent is required.
   */
  @post
  @route("exchange")
  exchange(
    @query userId: string,
    @query connectionName: string,
    @query channelId: string,
    @body body: TokenExchangeRequest,
  ): TokenResponse;

  /**
   * Get an existing token or a sign-in resource to initiate the OAuth flow.
   * Returns null (404) if neither is available.
   */
  @get
  @route("GetTokenOrSignInResource")
  getTokenOrSignInResource(
    @query userId: string,
    @query connectionName: string,
    @query channelId: string,
    @query state: string,
    @query code?: string,
    @query finalRedirect?: string,
    @query fwdUrl?: string,
  ): TokenOrSignInResourceResponse | void;
}

// ============================================================================
// Agent Sign-In Operations
// ============================================================================

@route("api/botsignin")
interface AgentSignIn {
  /**
   * Get the sign-in URL for initiating an OAuth flow.
   * Returns a plain-text URL string.
   */
  @get
  @route("GetSignInUrl")
  getSignInUrl(
    @query state: string,
    @query code_challenge?: string,
    @query emulatorUrl?: string,
    @query finalRedirect?: string,
  ): {
    @header contentType: "text/plain";
    @body body: string;
  };

  /**
   * Get the sign-in resource containing link and token exchange information.
   */
  @get
  @route("GetSignInResource")
  getSignInResource(
    @query state: string,
    @query code_challenge?: string,
    @query emulatorUrl?: string,
    @query finalRedirect?: string,
  ): SignInResource;
}
