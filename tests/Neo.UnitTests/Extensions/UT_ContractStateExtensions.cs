// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ContractStateExtensions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.SmartContract.Native;

namespace Neo.UnitTests.Extensions
{
    [TestClass]
    public class UT_ContractStateExtensions
    {
        private NeoSystem _system;

        [TestInitialize]
        public void Initialize()
        {
            _system = TestBlockchain.GetSystem();
        }

        [TestMethod]
        public void TestGetStorage()
        {
            var contractStorage = NativeContract.ContractManagement.FindContractStorage(_system.StoreView, NativeContract.NEO.Id);

            Assert.IsNotNull(contractStorage);

            var neoContract = NativeContract.ContractManagement.GetContractById(_system.StoreView, NativeContract.NEO.Id);

            contractStorage = neoContract.FindStorage(_system.StoreView);

            Assert.IsNotNull(contractStorage);

            contractStorage = neoContract.FindStorage(_system.StoreView, [20]);

            Assert.IsNotNull(contractStorage);

            UInt160 address = "0x9f8f056a53e39585c7bb52886418c7bed83d126b";
            var item = neoContract.GetStorage(_system.StoreView, [20, .. address.ToArray()]);

            Assert.IsNotNull(item);
            Assert.AreEqual(100_000_000, item.GetInteroperable<AccountState>().Balance);

            // Ensure GetInteroperableClone don't change nothing

            item.GetInteroperableClone<AccountState>().Balance = 123;
            Assert.AreEqual(100_000_000, item.GetInteroperable<AccountState>().Balance);
        }
    }
}
