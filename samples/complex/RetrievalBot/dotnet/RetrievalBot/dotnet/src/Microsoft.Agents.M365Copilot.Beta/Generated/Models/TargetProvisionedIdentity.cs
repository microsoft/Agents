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
    public partial class TargetProvisionedIdentity : global::Microsoft.Agents.M365Copilot.Beta.Models.Identity, IParsable
    #pragma warning restore CS1591
    {
        /// <summary>The identityType property</summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
#nullable enable
        public string? IdentityType
        {
            get { return BackingStore?.Get<string?>("identityType"); }
            set { BackingStore?.Set("identityType", value); }
        }
#nullable restore
#else
        public string IdentityType
        {
            get { return BackingStore?.Get<string>("identityType"); }
            set { BackingStore?.Set("identityType", value); }
        }
#endif
        /// <summary>
        /// Instantiates a new <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.TargetProvisionedIdentity"/> and sets the default values.
        /// </summary>
        public TargetProvisionedIdentity() : base()
        {
            OdataType = "#microsoft.graph.targetProvisionedIdentity";
        }
        /// <summary>
        /// Creates a new instance of the appropriate class based on discriminator value
        /// </summary>
        /// <returns>A <see cref="global::Microsoft.Agents.M365Copilot.Beta.Models.TargetProvisionedIdentity"/></returns>
        /// <param name="parseNode">The parse node to use to read the discriminator value and create the object</param>
        public static new global::Microsoft.Agents.M365Copilot.Beta.Models.TargetProvisionedIdentity CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new global::Microsoft.Agents.M365Copilot.Beta.Models.TargetProvisionedIdentity();
        }
        /// <summary>
        /// The deserialization information for the current model
        /// </summary>
        /// <returns>A IDictionary&lt;string, Action&lt;IParseNode&gt;&gt;</returns>
        public override IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
        {
            return new Dictionary<string, Action<IParseNode>>(base.GetFieldDeserializers())
            {
                { "identityType", n => { IdentityType = n.GetStringValue(); } },
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
            writer.WriteStringValue("identityType", IdentityType);
        }
    }
}
#pragma warning restore CS0618
