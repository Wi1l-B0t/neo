// Copyright (C) 2015-2025 The Neo Project.
//
// UT_FnDsa.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;

namespace Neo.UnitTests.Cryptography;

[TestClass]
public class UT_FnDsa
{
    [TestMethod]
    public void TestFalcon512()
    {
        var key = FnDsaPrivateKey.CreateFnDsa512();
        Assert.AreEqual(1280, key.ExportPrivateKey().Length);

        var publicKey = key.PublicKey;
        Assert.AreEqual(896, publicKey.ExportPublicKey().Length);

        var message = "hello world"u8;
        var signature = key.Sign(message.ToArray());

        var isValid = publicKey.Verify(message.ToArray(), signature);
        Assert.IsTrue(isValid);
    }
}
