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
    public partial class UserInformation : global::Microsoft.Agents.M365Copilot.Beta.Models.UserIdentity, IParsable
    #pragma warning restore CS1591
    {
        /// <summary>The accessScope property</summary>
        public global::Microsoft.Agents.M365Copilot.Beta.Models.AccessScope? AccessScope
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.AccessScope?>("accessScope"); }
            set { BackingStore?.Set("accessScope", value); }
        }
        /// <summary>The customAttributes property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public List<global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair>? CustomAttributes
        {
            get { return BackingStore?.Get<List<global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair>?>("customAttributes"); }
            set { BackingStore?.Set("customAttributes", value); }
        }
#nullable restore
#else
        public List<global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair> CustomAttributes
        {
            get { return BackingStore?.Get<List<global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair>>("customAttributes"); }
            set { BackingStore?.Set("customAttributes", value); }
        }
#endif
        /// <summary>The role property</summary>
        public global::Microsoft.Agents.M365Copilot.Beta.Models.MessageUserRole? Role
        {
            get { return BackingStore?.Get<global::Microsoft.Agents.M365Copilot.Beta.Models.MessageUserRole?>("role"); }
            set { BackingStore?.Set("role", value); }
        }
        /// <summary>
        /// Instantiates a new <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.UserInformation"/> and sets the default values.
        /// </summary>
        public UserInformation() : base()
        {
            OdataType = "#microsoft.graph.userInformation";
        }
        /// <summary>
        /// Creates a new instance of the appropriate class based on discriminator value
        /// </summary>
        /// <returns>A <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.UserInformation"/></returns>
        /// <param name="parseNode">The parse node to use to read the discriminator value and create the object</param>
        public static new global::Microsoft.Agents.M365Copilot.Beta.Models.UserInformation CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new global::Microsoft.Agents.M365Copilot.Beta.Models.UserInformation();
        }
        /// <summary>
        /// The deserialization information for the current model
        /// </summary>
        /// <returns>A IDictionary&lt;string, Action&lt;IParseNode&gt;&gt;</returns>
        public override IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>(base.GetFieldDeserializers())
            {
                { "accessScope", n => { AccessScope = n.GetEnumValue<global::Microsoft.Agents.M365Copilot.Beta.Models.AccessScope>(); } },
                { "customAttributes", n => { CustomAttributes = n.GetCollectionOfObjectValues<global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair>(global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair.CreateFromDiscriminatorValue)?.AsList(); } },
                { "role", n => { Role = n.GetEnumValue<global::Microsoft.Agents.M365Copilot.Beta.Models.MessageUserRole>(); } },
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
            writer.WriteEnumValue<global::Microsoft.Agents.M365Copilot.Beta.Models.AccessScope>("accessScope", AccessScope);
            writer.WriteCollectionOfObjectValues<global::Microsoft.Agents.M365Copilot.Beta.Models.KeyValuePair>("customAttributes", CustomAttributes);
            writer.WriteEnumValue<global::Microsoft.Agents.M365Copilot.Beta.Models.MessageUserRole>("role", Role);
        }
    }
}
#pragma warning restore CS0618
