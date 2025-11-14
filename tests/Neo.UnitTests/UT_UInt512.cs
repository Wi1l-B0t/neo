// Copyright (C) 2015-2025 The Neo Project.
//
// UT_UInt512.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Factories;
using Neo.IO;

namespace Neo.UnitTests;

[TestClass]
public class UT_UInt512
{
    [TestMethod]
    public void TestFail()
    {
        Assert.ThrowsExactly<FormatException>(() => _ = new UInt512(new byte[UInt512.Length + 1]));
    }

    [TestMethod]
    public void TestGernerator1()
    {
        var value = new UInt512();
        Assert.IsNotNull(value);
    }

    [TestMethod]
    public void TestGernerator2()
    {
        UInt512 value = new byte[64];
        Assert.IsNotNull(value);
        Assert.AreEqual(UInt512.Zero, value);
    }

    [TestMethod]
    public void TestGernerator3()
    {
        var buffer = new byte[64];
        buffer[63] = 0x01;

        var hex = buffer.ToHexString(reverse: true);
        UInt512 value = "0x" + hex;
        Assert.IsNotNull(value);
        Assert.AreEqual("0x" + hex, value.ToString());

        hex = "0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f40";
        value = hex;
        Assert.IsNotNull(value);
        Assert.AreEqual(hex, value.ToString());
    }

    [TestMethod]
    public void TestCompareTo()
    {
        byte[] temp = new byte[64];
        temp[63] = 0x01;

        var result = new UInt512(temp);
        Assert.AreEqual(0, UInt512.Zero.CompareTo(UInt512.Zero));
        Assert.AreEqual(-1, UInt512.Zero.CompareTo(result));
        Assert.AreEqual(1, result.CompareTo(UInt512.Zero));
        Assert.AreEqual(0, result.CompareTo(temp));
    }

    [TestMethod]
    public void TestDeserialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[20]);

        var value = new UInt512();
        Assert.ThrowsExactly<FormatException>(() =>
        {
            MemoryReader reader = new(stream.ToArray());
            ((ISerializable)value).Deserialize(ref reader);
        });
    }

    [TestMethod]
    public void TestEquals()
    {
        var temp = new byte[64];
        temp[63] = 0x01;

        var result = new UInt512(temp);
        Assert.IsTrue(UInt512.Zero.Equals(UInt512.Zero));
        Assert.IsFalse(UInt512.Zero.Equals(result));
        Assert.IsFalse(result.Equals(null));
    }

    [TestMethod]
    public void TestEquals1()
    {
        var temp1 = new UInt512();
        var temp2 = new UInt512();
        var temp3 = new UInt160();
        Assert.IsFalse(temp1.Equals(null));
        Assert.IsTrue(temp1.Equals(temp1));
        Assert.IsTrue(temp1.Equals(temp2));
        Assert.IsFalse(temp1.Equals(temp3));
    }

    [TestMethod]
    public void TestEquals2()
    {
        var temp1 = new UInt512();
        object temp2 = null;
        object temp3 = new();
        Assert.IsFalse(temp1.Equals(temp2));
        Assert.IsFalse(temp1.Equals(temp3));
    }

    [TestMethod]
    public void TestParse()
    {
        var action = () => UInt512.Parse(null);
        Assert.ThrowsExactly<FormatException>(() => action());

        var buffer = new byte[64];
        var hex = buffer.ToHexString(reverse: true);
        var result = UInt512.Parse("0x" + hex);
        Assert.AreEqual(UInt512.Zero, result);

        hex = buffer[..62].ToHexString(reverse: true);
        var action1 = () => UInt512.Parse(hex);
        Assert.ThrowsExactly<FormatException>(() => action1());

        hex = buffer.ToHexString(reverse: true);
        var result1 = UInt512.Parse(hex);
        Assert.AreEqual(UInt512.Zero, result1);
    }

    [TestMethod]
    public void TestTryParse()
    {
        Assert.IsFalse(UInt512.TryParse(null, out _));

        var buffer = new byte[64];
        var hex = "0x" + buffer.ToHexString(reverse: true);
        Assert.IsTrue(UInt512.TryParse(hex, out var temp));
        Assert.AreEqual(UInt512.Zero, temp);

        buffer[63] = 0x12;
        buffer[62] = 0x30;
        hex = "0x" + buffer.ToHexString(reverse: true);
        Assert.IsTrue(UInt512.TryParse(hex, out temp));
        Assert.AreEqual(hex, temp.ToString());

        hex = buffer[..62].ToHexString(reverse: true);
        Assert.IsFalse(UInt512.TryParse(hex, out _));

        hex = "0xKK" + buffer[..62].ToHexString(reverse: true);
        Assert.IsFalse(UInt512.TryParse(hex, out _));
    }

    [TestMethod]
    public void TestOperatorEqual()
    {
        Assert.IsFalse(new UInt512() == null);
        Assert.IsFalse(null == new UInt512());
    }

    [TestMethod]
    public void TestOperatorLarger()
    {
        Assert.IsFalse(UInt512.Zero > UInt512.Zero);
    }

    [TestMethod]
    public void TestOperatorLargerAndEqual()
    {
        Assert.IsTrue(UInt512.Zero >= UInt512.Zero);
    }

    [TestMethod]
    public void TestOperatorSmaller()
    {
        Assert.IsFalse(UInt512.Zero < UInt512.Zero);
    }

    [TestMethod]
    public void TestOperatorSmallerAndEqual()
    {
        Assert.IsTrue(UInt512.Zero <= UInt512.Zero);
    }

    [TestMethod]
    public void TestSpanAndSerialize()
    {
        var data = RandomNumberFactory.NextBytes(UInt512.Length);

        var value = new UInt512(data);
        var span = value.GetSpan();
        Assert.IsTrue(span.SequenceEqual(value.GetSpan().ToArray()));

        data = new byte[UInt512.Length];
        ((ISerializableSpan)value).Serialize(data.AsSpan());
        CollectionAssert.AreEqual(data, value.GetSpan().ToArray());
    }
}
