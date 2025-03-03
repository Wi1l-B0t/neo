// Copyright (C) 2015-2025 The Neo Project.
//
// UT_NefFile.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.IO;
using Neo.SmartContract;
using System;
using System.IO;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_NefFile
    {
        public NefFile file = new()
        {
            Compiler = "".PadLeft(32, ' '),
            Source = string.Empty,
            Tokens = Array.Empty<MethodToken>(),
            Script = new byte[] { 0x01, 0x02, 0x03 }
        };

        [TestInitialize]
        public void TestSetup()
        {
            file.CheckSum = NefFile.ComputeChecksum(file);
        }

        [TestMethod]
        public void TestDeserialize()
        {
            byte[] wrongMagic = { 0x00, 0x00, 0x00, 0x00 };
            using (MemoryStream ms = new(1024))
            using (BinaryWriter writer = new(ms))
            {
                ((ISerializable)file).Serialize(writer);
                ms.Seek(0, SeekOrigin.Begin);
                ms.Write(wrongMagic, 0, 4);
                ISerializable newFile = new NefFile();
                Assert.ThrowsExactly<FormatException>(() =>
                {
                    MemoryReader reader = new(ms.ToArray());
                    newFile.Deserialize(ref reader);
                    Assert.Fail();
                });
            }

            file.CheckSum = 0;
            using (MemoryStream ms = new(1024))
            using (BinaryWriter writer = new(ms))
            {
                ((ISerializable)file).Serialize(writer);
                ISerializable newFile = new NefFile();
                Assert.ThrowsExactly<FormatException>(() =>
                {
                    MemoryReader reader = new(ms.ToArray());
                    newFile.Deserialize(ref reader);
                    Assert.Fail();
                });
            }

            file.Script = Array.Empty<byte>();
            file.CheckSum = NefFile.ComputeChecksum(file);
            using (MemoryStream ms = new(1024))
            using (BinaryWriter writer = new(ms))
            {
                ((ISerializable)file).Serialize(writer);
                ISerializable newFile = new NefFile();
                Assert.ThrowsExactly<ArgumentException>(() =>
                {
                    MemoryReader reader = new(ms.ToArray());
                    newFile.Deserialize(ref reader);
                    Assert.Fail();
                });
            }

            file.Script = new byte[] { 0x01, 0x02, 0x03 };
            file.CheckSum = NefFile.ComputeChecksum(file);
            var data = file.ToArray();
            var newFile1 = data.AsSerializable<NefFile>();
            Assert.AreEqual(file.Compiler, newFile1.Compiler);
            Assert.AreEqual(file.CheckSum, newFile1.CheckSum);
            Assert.IsTrue(newFile1.Script.Span.SequenceEqual(file.Script.Span));
        }

        [TestMethod]
        public void TestGetSize()
        {
            Assert.AreEqual(4 + 32 + 32 + 2 + 1 + 2 + 4 + 4, file.Size);
        }

        [TestMethod]
        public void ParseTest()
        {
            var file = new NefFile()
            {
                Compiler = "".PadLeft(32, ' '),
                Source = string.Empty,
                Tokens = Array.Empty<MethodToken>(),
                Script = new byte[] { 0x01, 0x02, 0x03 }
            };

            file.CheckSum = NefFile.ComputeChecksum(file);

            var data = file.ToArray();
            file = data.AsSerializable<NefFile>();

            Assert.AreEqual("".PadLeft(32, ' '), file.Compiler);
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, file.Script.ToArray());
        }

        [TestMethod]
        public void LimitTest()
        {
            var file = new NefFile()
            {
                Compiler = "".PadLeft(byte.MaxValue, ' '),
                Source = string.Empty,
                Tokens = Array.Empty<MethodToken>(),
                Script = new byte[1024 * 1024],
                CheckSum = 0
            };

            // Wrong compiler

            Assert.ThrowsExactly<ArgumentException>(() => _ = file.ToArray());

            // Wrong script

            file.Compiler = "";
            file.Script = new byte[(1024 * 1024) + 1];
            var data = file.ToArray();

            Assert.ThrowsExactly<FormatException>(() => _ = data.AsSerializable<NefFile>());

            // Wrong script hash

            file.Script = new byte[1024 * 1024];
            data = file.ToArray();

            Assert.ThrowsExactly<FormatException>(() => _ = data.AsSerializable<NefFile>());

            // Wrong checksum

            file.Script = new byte[1024];
            data = file.ToArray();
            file.CheckSum = NefFile.ComputeChecksum(file) + 1;

            Assert.ThrowsExactly<FormatException>(() => _ = data.AsSerializable<NefFile>());
        }
    }
}
