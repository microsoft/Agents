// <auto-generated/>
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions.Store;
using System.Collections.Generic;
using System.IO;
using System;
namespace Microsoft.Agents.M365Copilot.Beta.Models
{
    [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    #pragma warning disable CS1591
    public partial class IdentitySet : IAdditionalDataHolder, IBackedModel, IParsable
    #pragma warning restore CS1591
    {
        /// <summary>Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.</summary>
        public IDictionary<string, object> AdditionalData
        {
            get { return BackingStore.Get<IDictionary<string, object>>("AdditionalData") ?? new Dictionary<string, object>(); }
            set { BackingStore.Set("AdditionalData", value); }
        }
        /// <summary>The application property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Microsoft.Agents.M365Copilot.Beta.Models.Identity? Application
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity?>("application"); }
            set { BackingStore?.Set("application", value); }
        }
#nullable restore
#else
        public global::Microsoft.Agents.M365Copilot.Beta.Models.Identity Application
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>("application"); }
            set { BackingStore?.Set("application", value); }
        }
#endif
        /// <summary>Stores model information.</summary>
        public IBackingStore BackingStore { get; private set; }
        /// <summary>The device property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Microsoft.Agents.M365Copilot.Beta.Models.Identity? Device
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity?>("device"); }
            set { BackingStore?.Set("device", value); }
        }
#nullable restore
#else
        public global::Microsoft.Agents.M365Copilot.Beta.Models.Identity Device
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>("device"); }
            set { BackingStore?.Set("device", value); }
        }
#endif
        /// <summary>The OdataType property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? OdataType
        {
            get { return BackingStore?.Get<string?>("@odata.type"); }
            set { BackingStore?.Set("@odata.type", value); }
        }
#nullable restore
#else
        public string OdataType
        {
            get { return BackingStore?.Get<string>("@odata.type"); }
            set { BackingStore?.Set("@odata.type", value); }
        }
#endif
        /// <summary>The user property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Microsoft.Agents.M365Copilot.Beta.Models.Identity? User
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity?>("user"); }
            set { BackingStore?.Set("user", value); }
        }
#nullable restore
#else
        public global::Microsoft.Agents.M365Copilot.Beta.Models.Identity User
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>("user"); }
            set { BackingStore?.Set("user", value); }
        }
#endif
        /// <summary>
        /// Instantiates a new <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.IdentitySet"/> and sets the default values.
        /// </summary>
        public IdentitySet()
        {
            BackingStore = BackingStoreFactorySingleton.Instance.CreateBackingStore();
            AdditionalData = new Dictionary<string, object>();
        }
        /// <summary>
        /// Creates a new instance of the appropriate class based on discriminator value
        /// </summary>
        /// <returns>A <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.IdentitySet"/></returns>
        /// <param name="parseNode">The parse node to use to read the discriminator value and create the object</param>
        public static global::Microsoft.Agents.M365Copilot.Beta.Models.IdentitySet CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            var mappingValue = parseNode.GetChildNode("@odata.type")?.GetStringValue();
            return mappingValue switch
            {
                "#microsoft.graph.aiInteractionMentionedIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.AiInteractionMentionedIdentitySet(),
                "#microsoft.graph.approvalIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.ApprovalIdentitySet(),
                "#microsoft.graph.chatMessageFromIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.ChatMessageFromIdentitySet(),
                "#microsoft.graph.chatMessageMentionedIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.ChatMessageMentionedIdentitySet(),
                "#microsoft.graph.chatMessageReactionIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.ChatMessageReactionIdentitySet(),
                "#microsoft.graph.communicationsIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.CommunicationsIdentitySet(),
                "#microsoft.graph.engagementIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.EngagementIdentitySet(),
                "#microsoft.graph.sharePointIdentitySet" => new global::Microsoft.Agents.M365Copilot.Beta.Models.SharePointIdentitySet(),
                _ => new global::Microsoft.Agents.M365Copilot.Beta.Models.IdentitySet(),
            };
        }
        /// <summary>
        /// The deserialization information for the current model
        /// </summary>
        /// <returns>A IDictionary&lt;string, Action&lt;IParseNode&gt;&gt;</returns>
        public virtual IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>
            {
                { "application", n => { Application = n.GetObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>(global::Microsoft.Agents.M365Copilot.Beta.Models.Identity.CreateFromDiscriminatorValue); } },
                { "device", n => { Device = n.GetObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>(global::Microsoft.Agents.M365Copilot.Beta.Models.Identity.CreateFromDiscriminatorValue); } },
                { "@odata.type", n => { OdataType = n.GetStringValue(); } },
                { "user", n => { User = n.GetObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>(global::Microsoft.Agents.M365Copilot.Beta.Models.Identity.CreateFromDiscriminatorValue); } },
            };
        }
        /// <summary>
        /// Serializes information the current object
        /// </summary>
        /// <param name="writer">Serialization writer to use to serialize this model</param>
        public virtual void Serialize(ISerializationWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>("application", Application);
            writer.WriteObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>("device", Device);
            writer.WriteStringValue("@odata.type", OdataType);
            writer.WriteObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.Identity>("user", User);
            writer.WriteAdditionalData(AdditionalData);
        }
    }
}
#pragma warning restore CS0618
