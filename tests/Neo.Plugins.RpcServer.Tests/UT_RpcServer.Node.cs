// Copyright (C) 2015-2025 The Neo Project.
//
// UT_RpcServer.Node.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
using Neo.UnitTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Neo.Plugins.RpcServer.Tests
{
    partial class UT_RpcServer
    {
        [TestMethod]
        public void TestGetConnectionCount()
        {
            var result = _rpcServer.GetConnectionCount();
            Assert.IsInstanceOfType(result, typeof(JNumber));
        }

        [TestMethod]
        public void TestGetPeers()
        {
            var settings = TestProtocolSettings.SoleNode;
            var neoSystem = new NeoSystem(settings, _memoryStoreProvider);
            var localNode = neoSystem.LocalNode.Ask<LocalNode>(new LocalNode.GetInstance()).Result;
            localNode.AddPeers(new List<IPEndPoint>() { new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 11332) });
            localNode.AddPeers(new List<IPEndPoint>() { new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 12332) });
            localNode.AddPeers(new List<IPEndPoint>() { new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 13332) });
            var rpcServer = new RpcServer(neoSystem, RpcServerSettings.Default);

            var result = rpcServer.GetPeers();
            Assert.IsInstanceOfType(result, typeof(JObject));
            var json = (JObject)result;
            Assert.IsTrue(json.ContainsProperty("unconnected"));
            Assert.AreEqual(3, (json["unconnected"] as JArray).Count);
            Assert.IsTrue(json.ContainsProperty("bad"));
            Assert.IsTrue(json.ContainsProperty("connected"));
        }

        [TestMethod]
        public void TestGetPeers_NoUnconnected()
        {
            // Setup a new system to ensure clean peer state
            var settings = TestProtocolSettings.SoleNode;
            var memoryStoreProvider = new TestMemoryStoreProvider(new MemoryStore());
            var neoSystem = new NeoSystem(settings, memoryStoreProvider);
            var rpcServer = new RpcServer(neoSystem, RpcServerSettings.Default);

            // Get peers immediately (should have no unconnected)
            var result = rpcServer.GetPeers();
            Assert.IsInstanceOfType(result, typeof(JObject));
            var json = (JObject)result;
            Assert.IsTrue(json.ContainsProperty("unconnected"));
            Assert.AreEqual(0, (json["unconnected"] as JArray).Count);
            Assert.IsTrue(json.ContainsProperty("bad"));
            Assert.IsTrue(json.ContainsProperty("connected"));
        }

        [TestMethod]
        public void TestGetPeers_NoConnected()
        {
            // Setup a new system to ensure clean peer state
            var settings = TestProtocolSettings.SoleNode;
            var memoryStoreProvider = new TestMemoryStoreProvider(new MemoryStore());
            var neoSystem = new NeoSystem(settings, memoryStoreProvider);
            var rpcServer = new RpcServer(neoSystem, RpcServerSettings.Default);

            // Get peers immediately (should have no connected)
            var result = rpcServer.GetPeers();
            Assert.IsInstanceOfType(result, typeof(JObject));
            var json = (JObject)result;
            Assert.IsTrue(json.ContainsProperty("unconnected"));
            Assert.IsTrue(json.ContainsProperty("bad"));
            Assert.IsTrue(json.ContainsProperty("connected"));
            Assert.AreEqual(0, (json["connected"] as JArray).Count); // Directly check connected count
        }

        [TestMethod]
        public void TestGetVersion()
        {
            var result = _rpcServer.GetVersion();
            Assert.IsInstanceOfType(result, typeof(JObject));

            var json = (JObject)result;
            Assert.IsTrue(json.ContainsProperty("tcpport"));
            Assert.IsTrue(json.ContainsProperty("nonce"));
            Assert.IsTrue(json.ContainsProperty("useragent"));

            Assert.IsTrue(json.ContainsProperty("protocol"));
            var protocol = (JObject)json["protocol"];
            Assert.IsTrue(protocol.ContainsProperty("addressversion"));
            Assert.IsTrue(protocol.ContainsProperty("network"));
            Assert.IsTrue(protocol.ContainsProperty("validatorscount"));
            Assert.IsTrue(protocol.ContainsProperty("msperblock"));
            Assert.IsTrue(protocol.ContainsProperty("maxtraceableblocks"));
            Assert.IsTrue(protocol.ContainsProperty("maxvaliduntilblockincrement"));
            Assert.IsTrue(protocol.ContainsProperty("maxtransactionsperblock"));
            Assert.IsTrue(protocol.ContainsProperty("memorypoolmaxtransactions"));
            Assert.IsTrue(protocol.ContainsProperty("standbycommittee"));
            Assert.IsTrue(protocol.ContainsProperty("seedlist"));
        }

        [TestMethod]
        public void TestGetVersion_HardforksStructure()
        {
            var result = _rpcServer.GetVersion();
            Assert.IsInstanceOfType(result, typeof(JObject));
            var json = (JObject)result;

            Assert.IsTrue(json.ContainsProperty("protocol"));
            var protocol = (JObject)json["protocol"];
            Assert.IsTrue(protocol.ContainsProperty("hardforks"));
            var hardforks = (JArray)protocol["hardforks"];

            // Check if there are any hardforks defined in settings
            if (hardforks.Count > 0)
            {
                Assert.IsTrue(hardforks.All(hf => hf is JObject)); // Each item should be an object
                foreach (JObject hfJson in hardforks)
                {
                    Assert.IsTrue(hfJson.ContainsProperty("name"));
                    Assert.IsTrue(hfJson.ContainsProperty("blockheight"));
                    Assert.IsInstanceOfType(hfJson["name"], typeof(JString));
                    Assert.IsInstanceOfType(hfJson["blockheight"], typeof(JNumber));
                    Assert.IsFalse(hfJson["name"].AsString().StartsWith("HF_")); // Check if prefix was stripped
                }
            }
            // If no hardforks are defined, the array should be empty
            else
            {
                Assert.AreEqual(0, _neoSystem.Settings.Hardforks.Count);
            }
        }

        #region SendRawTransaction Tests

        [TestMethod]
        public void TestSendRawTransaction_Normal()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            var txString = Convert.ToBase64String(tx.ToArray());

            var result = _rpcServer.SendRawTransaction(txString);
            Assert.IsInstanceOfType(result, typeof(JObject));
            Assert.IsTrue(((JObject)result).ContainsProperty("hash"));
        }

        [TestMethod]
        public void TestSendRawTransaction_InvalidTransactionFormat()
        {
            Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction("invalid_transaction_string"),
                "Should throw RpcException for invalid transaction format");
        }

        [TestMethod]
        public void TestSendRawTransaction_InsufficientBalance()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateInvalidTransaction(snapshot, _wallet, _walletAccount, TestUtils.InvalidTransactionType.InsufficientBalance);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for insufficient balance");
            Assert.AreEqual(RpcError.InsufficientFunds.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_InvalidSignature()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateInvalidTransaction(snapshot, _wallet, _walletAccount, TestUtils.InvalidTransactionType.InvalidSignature);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for invalid signature");
            Assert.AreEqual(RpcError.InvalidSignature.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_InvalidScript()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateInvalidTransaction(snapshot, _wallet, _walletAccount, TestUtils.InvalidTransactionType.InvalidScript);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for invalid script");
            Assert.AreEqual(RpcError.InvalidScript.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_InvalidAttribute()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateInvalidTransaction(snapshot, _wallet, _walletAccount, TestUtils.InvalidTransactionType.InvalidAttribute);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for invalid attribute");
            // Transaction with invalid attribute can not pass the Transaction deserialization
            // and will throw invalid params exception.
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_Oversized()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateInvalidTransaction(snapshot, _wallet, _walletAccount, TestUtils.InvalidTransactionType.Oversized);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for invalid format transaction");
            // Oversized transaction will not pass the deserialization.
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_Expired()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateInvalidTransaction(snapshot, _wallet, _walletAccount, TestUtils.InvalidTransactionType.Expired);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for expired transaction");
            Assert.AreEqual(RpcError.ExpiredTransaction.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_PolicyFailed()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            var txString = Convert.ToBase64String(tx.ToArray());
            NativeContract.Policy.BlockAccount(snapshot, _walletAccount.ScriptHash);
            snapshot.Commit();

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for conflicting transaction");
            Assert.AreEqual(RpcError.PolicyFailed.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_AlreadyInPool()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            _neoSystem.MemPool.TryAdd(tx, snapshot);
            var txString = Convert.ToBase64String(tx.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString),
                "Should throw RpcException for transaction already in memory pool");
            Assert.AreEqual(RpcError.AlreadyInPool.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_AlreadyInBlockchain()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var tx = TestUtils.CreateValidTx(snapshot, _wallet, _walletAccount);
            TestUtils.AddTransactionToBlockchain(snapshot, tx);
            snapshot.Commit();
            var txString = Convert.ToBase64String(tx.ToArray());
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(txString));
            Assert.AreEqual(RpcError.AlreadyExists.Code, exception.HResult);
        }

        #endregion

        #region SubmitBlock Tests

        [TestMethod]
        public void TestSubmitBlock_Normal()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var block = TestUtils.CreateBlockWithValidTransactions(snapshot, _wallet, _walletAccount, 1);
            var blockString = Convert.ToBase64String(block.ToArray());

            var result = _rpcServer.SubmitBlock(blockString);
            Assert.IsInstanceOfType(result, typeof(JObject));
            Assert.IsTrue(((JObject)result).ContainsProperty("hash"));
        }

        [TestMethod]
        public void TestSubmitBlock_InvalidBlockFormat()
        {
            string invalidBlockString = TestUtils.CreateInvalidBlockFormat();

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock(invalidBlockString),
                "Should throw RpcException for invalid block format");

            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
            StringAssert.Contains(exception.Message, "Invalid Block Format");
        }

        [TestMethod]
        public void TestSubmitBlock_AlreadyExists()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var block = TestUtils.CreateBlockWithValidTransactions(snapshot, _wallet, _walletAccount, 1);
            TestUtils.BlocksAdd(snapshot, block.Hash, block);
            snapshot.Commit();
            var blockString = Convert.ToBase64String(block.ToArray());

            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock(blockString),
                "Should throw RpcException when block already exists");
            Assert.AreEqual(RpcError.AlreadyExists.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSubmitBlock_InvalidBlock()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var block = TestUtils.CreateBlockWithValidTransactions(snapshot, _wallet, _walletAccount, 1);
            block.Header.Witness = Witness.Empty;

            var blockString = Convert.ToBase64String(block.ToArray());
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock(blockString),
                "Should throw RpcException for invalid block");
            Assert.AreEqual(RpcError.VerificationFailed.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSubmitBlock_InvalidPrevHash()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var block = TestUtils.CreateBlockWithValidTransactions(snapshot, _wallet, _walletAccount, 1);
            // Intentionally set wrong PrevHash
            block.Header.PrevHash = TestUtils.RandomUInt256();

            var blockString = Convert.ToBase64String(block.ToArray());
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock(blockString),
                "Should throw RpcException for invalid previous hash");
            // The specific error might depend on where verification fails, VerificationFailed is a likely candidate
            Assert.AreEqual(RpcError.VerificationFailed.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSubmitBlock_InvalidIndex()
        {
            var snapshot = _neoSystem.GetSnapshotCache();
            var block = TestUtils.CreateBlockWithValidTransactions(snapshot, _wallet, _walletAccount, 1);
            // Intentionally set wrong Index
            block.Header.Index = NativeContract.Ledger.CurrentIndex(snapshot) + 10;

            var blockString = Convert.ToBase64String(block.ToArray());
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock(blockString),
                "Should throw RpcException for invalid block index");
            // The specific error might depend on where verification fails, VerificationFailed is likely
            Assert.AreEqual(RpcError.VerificationFailed.Code, exception.HResult);
        }

        #endregion

        #region Edge Cases and Error Handling

        [TestMethod]
        public void TestSendRawTransaction_NullInput()
        {
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction((string)null),
                "Should throw RpcException for null input");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSendRawTransaction_EmptyInput()
        {
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SendRawTransaction(string.Empty),
                "Should throw RpcException for empty input");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSubmitBlock_NullInput()
        {
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock((string)null),
                "Should throw RpcException for null input");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }

        [TestMethod]
        public void TestSubmitBlock_EmptyInput()
        {
            var exception = Assert.ThrowsExactly<RpcException>(() => _ = _rpcServer.SubmitBlock(string.Empty),
                "Should throw RpcException for empty input");
            Assert.AreEqual(RpcError.InvalidParams.Code, exception.HResult);
        }

        #endregion
    }
}
