// Copyright (C) 2015-2025 The Neo Project.
//
// UT_ContractEventDescriptor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract.Manifest;

namespace Neo.UnitTests.SmartContract.Manifest
{
    [TestClass]
    public class UT_ContractEventDescriptor
    {
        [TestMethod]
        public void TestFromJson()
        {
            var expected = new ContractEventDescriptor
            {
                Name = "AAA",
                Parameters = [],
            };
            var actual = ContractEventDescriptor.FromJson(expected.ToJson());
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(0, actual.Parameters.Length);
        }
    }
}
