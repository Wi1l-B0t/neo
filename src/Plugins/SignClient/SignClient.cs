// Copyright (C) 2015-2025 The Neo Project.
//
// SignClient.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P;
using Neo.Sign;
using Neo.SmartContract;
using Servicepb;
using Signpb;
using System.Net;


namespace Neo.Plugins.SignClient
{
    /// <summary>
    /// A signer that uses a client to sign transactions.
    /// </summary>
    public class SignClient : Plugin, ISigner
    {
        private GrpcChannel? _channel;

        private SecureSign.SecureSignClient? _client;

        private Settings? _settings;

        public override string Description => "Signer plugin for signer service.";

        public override string ConfigFile => System.IO.Path.Combine(RootPath, "SignClient.json");

        public SignClient() { }

        public SignClient(Settings settings)
        {
            Reset(settings);
        }

        private void Reset(Settings settings)
        {
            _settings = settings;

            var methodConfig = new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 3,
                    InitialBackoff = TimeSpan.FromMilliseconds(50),
                    MaxBackoff = TimeSpan.FromMilliseconds(200),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = {
                        StatusCode.Cancelled,
                        StatusCode.DeadlineExceeded,
                        StatusCode.ResourceExhausted,
                        StatusCode.Unavailable,
                        StatusCode.Aborted,
                        StatusCode.Internal,
                        StatusCode.DataLoss,
                        StatusCode.Unknown
                    }
                }
            };

            // sign server run on localhost, so http is ok
            var address = new IPEndPoint(IPAddress.Parse(_settings.Host), _settings.Port);
            var channel = GrpcChannel.ForAddress($"http://{address}", new GrpcChannelOptions
            {
                ServiceConfig = new ServiceConfig { MethodConfigs = { methodConfig } }
            });

            _channel?.Dispose();
            _channel = channel;
            _client = new SecureSign.SecureSignClient(_channel);
        }

        /// <summary>
        /// Get account status
        /// </summary>
        /// <param name="hexPublicKey">The hex public key, compressed or uncompressed</param>
        [ConsoleCommand("get account status", Category = "Signer Commands", Description = "Get account status")]
        public void AccountStatusCommand(string hexPublicKey)
        {
            var publicKey = ECPoint.DecodePoint(hexPublicKey.HexToBytes(), ECCurve.Secp256r1);
            var status = GetAccountStatus(publicKey);
            ConsoleHelper.Info("", $"Account status: {status}");
        }

        private AccountStatus GetAccountStatus(ECPoint publicKey)
        {
            if (_client is null) throw new SignException("No signer service is connected");

            try
            {
                var output = _client.GetAccountStatus(new()
                {
                    PublicKey = ByteString.CopyFrom(publicKey.EncodePoint(true))
                });
                return output.Status;
            }
            catch (RpcException ex)
            {
                throw new SignException($"Get account status: {ex.Status}", ex);
            }
        }

        /// <inheritdoc/>
        public bool ContainsSignable(ECPoint publicKey)
        {
            var status = GetAccountStatus(publicKey);
            return status == AccountStatus.Single || status == AccountStatus.Multiple;
        }

        internal bool Sign(ContractParametersContext context, IEnumerable<AccountSigns> signs)
        {
            var succeed = false;
            foreach (var (accountSigns, scriptHash) in signs.Zip(context.ScriptHashes))
            {
                var accountStatus = accountSigns.Status;
                if (accountStatus == AccountStatus.NoSuchAccount || accountStatus == AccountStatus.NoPrivateKey)
                {
                    succeed |= context.AddWithScriptHash(scriptHash);
                    continue;
                }

                var accountContract = Contract.Create(
                    accountSigns.Contract?.Parameters?.Select(p => (ContractParameterType)p).ToArray() ?? [],
                    accountSigns.Contract?.Script?.ToByteArray() ?? []);
                if (accountStatus == AccountStatus.Multiple)
                {
                    // foreach (var accountSign in accountSigns.Signs)
                    throw new NotImplementedException("Multiple account signing is not implemented");
                }
                else if (accountStatus == AccountStatus.Single)
                {
                    if (accountSigns.Signs is null || accountSigns.Signs.Count != 1)
                        throw new SignException($"Sign context: single account but {accountSigns.Signs?.Count} signs");
                    try
                    {
                        var sign = accountSigns.Signs[0];
                        var publicKey = ECPoint.DecodePoint(sign.PublicKey.Span, ECCurve.Secp256r1);
                        succeed |= context.AddSignature(accountContract, publicKey, sign.Signature.ToByteArray());
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                }
            }
            return succeed;
        }

        /// <inheritdoc/>
        public bool Sign(ContractParametersContext context)
        {
            if (_client is null) throw new SignException("No signer service is connected");

            try
            {
                var signData = context.Verifiable.GetSignData(context.Network);
                var output = _client.SignWithScriptHashes(new()
                {
                    SignData = ByteString.CopyFrom(signData),
                    ScriptHashes = { context.ScriptHashes.Select(h160 => ByteString.CopyFrom(h160.GetSpan())) }
                });

                int signCount = output.Signs.Count, hashCount = context.ScriptHashes.Count;
                if (signCount != hashCount)
                {
                    throw new SignException($"Sign context: Signs.Count({signCount}) != Hashes.Count({hashCount})");
                }
                return Sign(context, output.Signs);
            }
            catch (RpcException ex)
            {
                throw new SignException($"Sign context: {ex.Status}", ex);
            }
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> Sign(byte[] signData, ECPoint publicKey)
        {
            if (_client is null) throw new SignException("No signer service is connected");

            try
            {
                var output = _client.SignWithPublicKey(new()
                {
                    SignData = ByteString.CopyFrom(signData),
                    PublicKey = ByteString.CopyFrom(publicKey.EncodePoint(true)),
                });
                return output.Signature.Memory;
            }
            catch (RpcException ex)
            {
                throw new SignException($"Sign with public key: {ex.Status}", ex);
            }
        }

        /// <inheritdoc/>
        protected override void Configure()
        {
            Reset(new Settings(GetConfiguration()));
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _channel?.Dispose();
            base.Dispose();
        }

    }
}
