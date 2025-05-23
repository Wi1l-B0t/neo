// Copyright (C) 2015-2025 The Neo Project.
//
// RpcResponse.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcResponse
    {
        public JToken Id { get; set; }

        public string JsonRpc { get; set; }

        public RpcResponseError Error { get; set; }

        public JToken Result { get; set; }

        public string RawResponse { get; set; }

        public static RpcResponse FromJson(JObject json)
        {
            var response = new RpcResponse
            {
                Id = json["id"],
                JsonRpc = json["jsonrpc"].AsString(),
                Result = json["result"]
            };

            if (json["error"] != null)
            {
                response.Error = RpcResponseError.FromJson((JObject)json["error"]);
            }

            return response;
        }

        public JObject ToJson()
        {
            return new()
            {
                ["id"] = Id,
                ["jsonrpc"] = JsonRpc,
                ["error"] = Error?.ToJson(),
                ["result"] = Result
            };
        }
    }

    public class RpcResponseError
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public JToken Data { get; set; }

        public static RpcResponseError FromJson(JObject json)
        {
            return new RpcResponseError
            {
                Code = (int)json["code"].AsNumber(),
                Message = json["message"].AsString(),
                Data = json["data"],
            };
        }

        public JObject ToJson()
        {
            return new()
            {
                ["code"] = Code,
                ["message"] = Message,
                ["data"] = Data
            };
        }
    }
}
