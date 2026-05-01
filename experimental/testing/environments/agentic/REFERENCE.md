Option B: Microsoft Graph API (For Beta Permissions)
Use this method if Microsoft Entra admin center doesn't show AgentIdentityBlueprint.* permissions.

 Warning

If you use this API method, don't use Microsoft Entra admin center's "Grant admin consent" button afterward. The API method grants admin consent automatically, and using the Microsoft Entra admin center button deletes your beta permissions. For more information, see Beta permissions disappear.

Open Graph Explorer.

Sign in with your admin account (Application Administrator or Cloud Application Administrator).

Grant admin consent by using Graph API. To complete this step, you need:

Service principal ID. You need a SP_OBJECT_ID variable value.
Graph resource ID. You need a GRAPH_RESOURCE_ID variable value.
Create (or update) delegated permissions by using the oAuth2PermissionGrant resource type with the SP_OBJECT_ID and GRAPH_RESOURCE_ID variable values.
Use the information in the following sections to complete these steps.

Get your service principal ID
A service principal is your app's identity in your tenant. You need it before you can grant permissions through the API.

Set the Graph Explorer method to GET and use this URL. Replace <YOUR_CLIENT_APP_ID> with your actual Application client ID from Step 3: Copy Application (client) ID:

HTTP
https://graph.microsoft.com/v1.0/servicePrincipals?$filter=appId eq '<YOUR_CLIENT_APP_ID>'&$select=id
Select Run query.

If the query succeeds, the value returned is your SP_OBJECT_ID.

If the query fails with a permissions error, select the Modify permissions tab, consent to the required permissions, and then select Run query again. The value returned is your SP_OBJECT_ID.

If the query returns empty results ("value": []), create the service principal by using the following steps:

Set method to POST and use this URL:

HTTP
https://graph.microsoft.com/v1.0/servicePrincipals
Request Body (replace YOUR_CLIENT_APP_ID with your actual Application client ID):

JSON
{
   "appId": "YOUR_CLIENT_APP_ID"
}
Select Run query. You should get a 201 Created response. The id value returned is your SP_OBJECT_ID.

Get your Graph resource ID
Set the Graph Explorer method to GET and use this URL:

HTTP
https://graph.microsoft.com/v1.0/servicePrincipals?$filter=appId eq '00000003-0000-0000-c000-000000000000'&$select=id
Select Run query.

If the query succeeds, copy the id value. This value is your GRAPH_RESOURCE_ID.
If the query fails with a permissions error, select the Modify permissions tab, consent to the required permissions, and then select Run query again. Copy the id value. This value is your GRAPH_RESOURCE_ID.
Create delegated permissions
This API call grants tenant-wide admin consent for all thirteen permissions, including the beta permissions that aren't visible in Microsoft Entra admin center.

Set the Graph Explorer method to POST and use this URL and request body:

HTTP
https://graph.microsoft.com/v1.0/oauth2PermissionGrants
Request Body:

JSON
{
"clientId": "<SP_OBJECT_ID>",
"consentType": "AllPrincipals",
"principalId": null,
"resourceId": "<GRAPH_RESOURCE_ID>",
"scope": "AgentIdentityBlueprintPrincipal.Create AgentIdentityBlueprint.ReadWrite.All AgentIdentityBlueprint.UpdateAuthProperties.All AgentIdentityBlueprint.AddRemoveCreds.All AgentIdentityBlueprint.DeleteRestore.All AgentInstance.ReadWrite.All AgentIdentity.Create.All AgentIdentity.DeleteRestore.All AgentIdentity.Read.All AgentRegistration.ReadWrite.All DelegatedPermissionGrant.ReadWrite.All Directory.Read.All User.Read"
}
Select Run query.

If you get 201 Created response: Success! The scope field in the response shows all thirteen permission names. You're done.
If the query fails with a permissions error (likely DelegatedPermissionGrant.ReadWrite.All), select the Modify permissions tab, consent to DelegatedPermissionGrant.ReadWrite.All, and then select Run query again.
If you get error Request_MultipleObjectsWithSameKeyValue: A grant already exists. Maybe someone added permissions earlier. See the following Update delegated permissions.
 Warning

The consentType: "AllPrincipals" in the POST request already grants tenant-wide admin consent. DO NOT select "Grant admin consent" in Microsoft Entra admin center after using this API method - doing so deletes your beta permissions because the Microsoft Entra admin center can't see beta permissions and overwrites your API-granted consent with only the visible permissions.

Update delegated permissions
When you get a Request_MultipleObjectsWithSameKeyValue error by using the steps to Create delegated permissions, use these steps to update the delegated permissions.

Set the Graph Explorer method to GET and use this URL:

HTTP
https://graph.microsoft.com/v1.0/oauth2PermissionGrants?$filter=clientId eq 'SP_OBJECT_ID_FROM_ABOVE'
Select Run query. Copy the id value from the response. This value is YOUR_GRANT_ID.

Set the Graph Explorer method to PATCH and use this URL with YOUR_GRANT_ID.

HTTP
https://graph.microsoft.com/v1.0/oauth2PermissionGrants/<YOUR_GRANT_ID>
Request Body:

JSON
{
   "scope": "AgentIdentityBlueprintPrincipal.Create AgentIdentityBlueprint.ReadWrite.All AgentIdentityBlueprint.UpdateAuthProperties.All AgentIdentityBlueprint.AddRemoveCreds.All AgentIdentityBlueprint.DeleteRestore.All AgentInstance.ReadWrite.All AgentIdentity.Create.All AgentIdentity.DeleteRestore.All AgentIdentity.Read.All AgentRegistration.ReadWrite.All DelegatedPermissionGrant.ReadWrite.All Directory.Read.All User.Read"
}
Select Run query. You should get a 200 OK response with all thirteen permissions in the scope field.