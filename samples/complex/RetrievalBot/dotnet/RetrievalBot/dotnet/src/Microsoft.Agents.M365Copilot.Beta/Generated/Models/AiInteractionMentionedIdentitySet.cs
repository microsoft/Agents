// <auto-generated/>
#pragma warning disable CS0618
using Microsoft.Kiota.Abstractions.Extensions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Collections.Generic;
using System.IO;
using System;
namespace Microsoft.Agents.M365Copilot.Beta.Models
{
    [global::System.CodeDom.Compiler.GeneratedCode("Kiota", "1.0.0")]
    #pragma warning disable CS1591
    public partial class AiInteractionMentionedIdentitySet : global::Microsoft.Agents.M365Copilot.Beta.Models.IdentitySet, IParsable
    #pragma warning restore CS1591
    {
        /// <summary>The conversation property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity? Conversation
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity?>("conversation"); }
            set { BackingStore?.Set("conversation", value); }
        }
#nullable restore
#else
        public global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity Conversation
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity>("conversation"); }
            set { BackingStore?.Set("conversation", value); }
        }
#endif
        /// <summary>The tag property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity? Tag
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity?>("tag"); }
            set { BackingStore?.Set("tag", value); }
        }
#nullable restore
#else
        public global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity Tag
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity>("tag"); }
            set { BackingStore?.Set("tag", value); }
        }
#endif
        /// <summary>
        /// Instantiates a new <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.AiInteractionMentionedIdentitySet"/> and sets the default values.
        /// </summary>
        public AiInteractionMentionedIdentitySet() : base()
        {
            OdataType = "#microsoft.graph.aiInteractionMentionedIdentitySet";
        }
        /// <summary>
        /// Creates a new instance of the appropriate class based on discriminator value
        /// </summary>
        /// <returns>A <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.AiInteractionMentionedIdentitySet"/></returns>
        /// <param name="parseNode">The parse node to use to read the discriminator value and create the object</param>
        public static new global::Microsoft.Agents.M365Copilot.Beta.Models.AiInteractionMentionedIdentitySet CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new global::Microsoft.Agents.M365Copilot.Beta.Models.AiInteractionMentionedIdentitySet();
        }
        /// <summary>
        /// The deserialization information for the current model
        /// </summary>
        /// <returns>A IDictionary&lt;string, Action&lt;IParseNode&gt;&gt;</returns>
        public override IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>(base.GetFieldDeserializers())
            {
                { "conversation", n => { Conversation = n.GetObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity>(global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity.CreateFromDiscriminatorValue); } },
                { "tag", n => { Tag = n.GetObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity>(global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity.CreateFromDiscriminatorValue); } },
            };
        }
        /// <summary>
        /// Serializes information the current object
        /// </summary>
        /// <param name="writer">Serialization writer to use to serialize this model</param>
        public override void Serialize(ISerializationWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            base.Serialize(writer);
            writer.WriteObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkConversationIdentity>("conversation", Conversation);
            writer.WriteObjectValue<global::Microsoft.Agents.M365Copilot.Beta.Models.TeamworkTagIdentity>("tag", Tag);
        }
    }
}
#pragma warning restore CS0618
