// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NeoTokenExtensions.cs file belongs to the neo project and is free
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
using System.Linq;

namespace Neo.UnitTests.Extensions
{
    [TestClass]
    public class UT_NeoTokenExtensions
    {
        private NeoSystem _system;

        [TestInitialize]
        public void Initialize()
        {
            _system = TestBlockchain.GetSystem();
        }

        [TestMethod]
        public void TestGetAccounts()
        {
            UInt160 expected = "0x9f8f056a53e39585c7bb52886418c7bed83d126b";

            var accounts = NativeContract.NEO.GetAccounts(_system.StoreView);
            var actual = accounts.FirstOrDefault();

            Assert.AreEqual(expected, actual.Address);
            Assert.AreEqual(100000000, actual.Balance);
        }
    }
}
