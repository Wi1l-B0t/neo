// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ContractPermissionDescriptor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Json;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Neo.UnitTests.SmartContract.Manifest
{
    [TestClass]
    public class UT_ContractPermissionDescriptor
    {
        [TestMethod]
        public void TestCreateByECPointAndIsWildcard()
        {
            byte[] privateKey = new byte[32];
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            KeyPair key = new KeyPair(privateKey);
            ContractPermissionDescriptor contractPermissionDescriptor = ContractPermissionDescriptor.Create(key.PublicKey);
            Assert.IsNotNull(contractPermissionDescriptor);
            Assert.AreEqual(key.PublicKey, contractPermissionDescriptor.Group);
            Assert.IsFalse(contractPermissionDescriptor.IsWildcard);
        }

        [TestMethod]
        public void TestContractPermissionDescriptorFromAndToJson()
        {
            byte[] privateKey = new byte[32];
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            KeyPair key = new KeyPair(privateKey);
            ContractPermissionDescriptor temp = ContractPermissionDescriptor.Create(key.PublicKey);
            ContractPermissionDescriptor result = ContractPermissionDescriptor.FromJson(temp.ToJson());
            Assert.IsNull(result.Hash);
            Assert.AreEqual(result.Group, result.Group);
            Assert.ThrowsExactly<FormatException>(() => _ = ContractPermissionDescriptor.FromJson(string.Empty));
        }

        [TestMethod]
        public void TestContractManifestFromJson()
        {
            Assert.ThrowsExactly<NullReferenceException>(() => _ = ContractManifest.FromJson(new JObject()));
            var jsonFiles = Directory.GetFiles(Path.Combine("SmartContract", "Manifest", "TestFile"));
            foreach (var item in jsonFiles)
            {
                var json = JObject.Parse(File.ReadAllText(item)) as JObject;
                var manifest = ContractManifest.FromJson(json);
                Assert.AreEqual(manifest.ToJson().ToString(), json.ToString());
            }
        }

        [TestMethod]
        public void TestEquals()
        {
            var descriptor1 = ContractPermissionDescriptor.CreateWildcard();
            var descriptor2 = ContractPermissionDescriptor.Create(LedgerContract.NEO.Hash);

            Assert.AreNotEqual(descriptor1, descriptor2);

            var descriptor3 = ContractPermissionDescriptor.Create(LedgerContract.NEO.Hash);

            Assert.AreEqual(descriptor2, descriptor3);
        }
    }
}
