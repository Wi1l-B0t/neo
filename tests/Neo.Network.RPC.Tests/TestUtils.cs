// Copyright (C) 2015-2025 The Neo Project.
//
// TestUtils.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Network.RPC.Tests
{
    internal static class TestUtils
    {
        public readonly static List<RpcTestCase> RpcTestCases = ((JArray)JToken.Parse(File.ReadAllText("RpcTestCases.json"))).Select(p => RpcTestCase.FromJson((JObject)p)).ToList();

        public static Block GetBlock(int txCount)
        {
            return new Block
            {
                Header = new Header
                {
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    NextConsensus = UInt160.Zero,
                    Witness = Witness.Empty,
                },
                Transactions = Enumerable.Range(0, txCount).Select(p => GetTransaction()).ToArray()
            };
        }

        public static Header GetHeader()
        {
            return GetBlock(0).Header;
        }

        public static Transaction GetTransaction()
        {
            return new Transaction
            {
                Script = new byte[1],
                Signers = [new() { Account = UInt160.Zero }],
                Attributes = [],
                Witnesses = [Witness.Empty],
            };
        }
    }

    internal class RpcTestCase
    {
        public string Name { get; set; }
        public RpcRequest Request { get; set; }
        public RpcResponse Response { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["Name"] = Name,
                ["Request"] = Request.ToJson(),
                ["Response"] = Response.ToJson(),
            };
        }

        public static RpcTestCase FromJson(JObject json)
        {
            return new RpcTestCase
            {
                Name = json["Name"].AsString(),
                Request = RpcRequest.FromJson((JObject)json["Request"]),
                Response = RpcResponse.FromJson((JObject)json["Response"]),
            };
        }

    }
}
