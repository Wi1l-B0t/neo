// Copyright (C) 2015-2025 The Neo Project.
//
// VersionPayload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Capabilities;
using System;
using System.IO;
using System.Linq;

namespace Neo.Network.P2P.Payloads
{
    /// <summary>
    /// Sent when a connection is established.
    /// </summary>
    public class VersionPayload : ISerializable
    {
        /// <summary>
        /// Indicates the maximum number of capabilities contained in a <see cref="VersionPayload"/>.
        /// </summary>
        public const int MaxCapabilities = 32;

        /// <summary>
        /// The magic number of the network.
        /// </summary>
        public uint Network;

        /// <summary>
        /// The protocol version of the node.
        /// </summary>
        public uint Version;

        /// <summary>
        /// The time when connected to the node (UTC).
        /// </summary>
        public uint Timestamp;

        /// <summary>
        /// A random number used to identify the node.
        /// </summary>
        public uint Nonce;

        /// <summary>
        /// A <see cref="string"/> used to identify the client software of the node.
        /// </summary>
        public string UserAgent;

        /// <summary>
        /// True if allow compression
        /// </summary>
        public bool AllowCompression;

        /// <summary>
        /// The capabilities of the node.
        /// </summary>
        public NodeCapability[] Capabilities;

        public int Size =>
            sizeof(uint) +              // Network
            sizeof(uint) +              // Version
            sizeof(uint) +              // Timestamp
            sizeof(uint) +              // Nonce
            UserAgent.GetVarSize() +    // UserAgent
            Capabilities.GetVarSize();  // Capabilities

        /// <summary>
        /// Creates a new instance of the <see cref="VersionPayload"/> class.
        /// </summary>
        /// <param name="network">The magic number of the network.</param>
        /// <param name="nonce">The random number used to identify the node.</param>
        /// <param name="userAgent">The <see cref="string"/> used to identify the client software of the node.</param>
        /// <param name="capabilities">The capabilities of the node.</param>
        /// <returns></returns>
        public static VersionPayload Create(uint network, uint nonce, string userAgent, params NodeCapability[] capabilities)
        {
            var ret = new VersionPayload
            {
                Network = network,
                Version = LocalNode.ProtocolVersion,
                Timestamp = DateTime.UtcNow.ToTimestamp(),
                Nonce = nonce,
                UserAgent = userAgent,
                Capabilities = capabilities,
                // Computed
                AllowCompression = !capabilities.Any(u => u is DisableCompressionCapability)
            };

            return ret;
        }

        void ISerializable.Deserialize(ref MemoryReader reader)
        {
            Network = reader.ReadUInt32();
            Version = reader.ReadUInt32();
            Timestamp = reader.ReadUInt32();
            Nonce = reader.ReadUInt32();
            UserAgent = reader.ReadVarString(1024);

            // Capabilities
            Capabilities = new NodeCapability[reader.ReadVarInt(MaxCapabilities)];
            for (int x = 0, max = Capabilities.Length; x < max; x++)
                Capabilities[x] = NodeCapability.DeserializeFrom(ref reader);
            var capabilities = Capabilities.Where(c => c is not UnknownCapability);
            if (capabilities.Select(p => p.Type).Distinct().Count() != capabilities.Count())
                throw new FormatException();

            AllowCompression = !capabilities.Any(u => u is DisableCompressionCapability);
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(Network);
            writer.Write(Version);
            writer.Write(Timestamp);
            writer.Write(Nonce);
            writer.WriteVarString(UserAgent);
            writer.Write(Capabilities);
        }
    }
}
