// Copyright (C) 2015-2025 The Neo Project.
//
// UInt512.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Neo
{
    /// <summary>
    /// Represents a 512-bit unsigned integer in little-endian.
    /// </summary>
    public class UInt512 : IComparable, IComparable<UInt512>, IEquatable<UInt512>, ISerializable, ISerializableSpan
    {
        /// <summary>
        /// The length in bytes of UInt512 values.
        /// </summary>
        public const int Length = 64;

        public static UInt512 Zero => new();

        [InlineArray(8)]
        private struct Holder
        {
            internal ulong Value0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<ulong> AsSpan() => MemoryMarshal.CreateSpan(ref Value0, 8);
        }

        private Holder _holder = new();

        /// <summary>
        /// The size of the UInt512 in bytes.
        /// </summary>
        public int Size => Length;

        /// <summary>
        /// Initializes a new instance with zero value.
        /// </summary>
        public UInt512() { }

        /// <summary>
        /// Initializes a new instance from a span of bytes in little-endian.
        /// </summary>
        /// <param name="value">The value of the UInt512.</param>
        /// <exception cref="FormatException">The argument value is not the UInt512 length in bytes.</exception>
        public UInt512(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
                throw new FormatException($"Invalid UInt512 length: expected {Length} bytes, but got {value.Length} bytes");

            var span = MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, byte>(ref _holder.Value0), Length);
            value.CopyTo(span);
        }


        /// <summary>
        /// Gets a ReadOnlySpan that represents the current value in little-endian.
        /// </summary>
        /// <returns>A ReadOnlySpan that represents the current value in little-endian.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetSpan()
        {
            if (BitConverter.IsLittleEndian)
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref _holder.Value0), Length);

            var span = _holder.AsSpan();
            Span<byte> buffer = new byte[Length];
            for (int i = 0; i < span.Length; i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(buffer[(i * sizeof(ulong))..], span[i]);
            }
            return buffer;
        }

        public int CompareTo(object? obj)
        {
            if (ReferenceEquals(obj, this)) return 0;
            return CompareTo(obj as UInt512);
        }

        public int CompareTo(UInt512? other)
        {
            if (other is null) return 1;

            var thisSpan = _holder.AsSpan();
            var otherSpan = other._holder.AsSpan();
            for (int i = thisSpan.Length - 1; i >= 0; i--) // compare each ulong in the holder in reverse order
            {
                var order = thisSpan[i].CompareTo(otherSpan[i]);
                if (order != 0) return order;
            }
            return 0;
        }

        public override bool Equals(object? obj) => ReferenceEquals(obj, this) || Equals(obj as UInt512);

        public bool Equals(UInt512? other) => other != null && _holder.AsSpan().SequenceEqual(other._holder.AsSpan());

        public override int GetHashCode() => _holder.AsSpan().GetHashCode();

        /// <summary>
        /// Returns a string representation of the UInt512 in hexadecimal format with the '0x' prefix.
        /// </summary>
        /// <returns>A string representation of the UInt512 in hexadecimal format with the '0x' prefix.</returns>
        public override string ToString() => "0x" + GetSpan().ToHexString(reverse: true);

        /// <summary>
        /// Tries to parse a UInt512 from a <see cref="string"/>.
        /// The string must be a valid hexadecimal string of length <see cref="Length"/> * 2 without the '0x' prefix.
        /// </summary>
        /// <param name="value">The <see cref="string"/> to parse.</param>
        /// <param name="result">The parsed UInt512.</param>
        /// <returns><see langword="true"/> if the <see cref="string"/> was parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(string value, [NotNullWhen(true)] out UInt512? result)
        {
            result = null;
            var data = value.AsSpan().TrimStartIgnoreCase("0x");
            if (data.Length != Length * 2) return false;
            try
            {
                result = new UInt512(data.HexToBytesReversed());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses a UInt512 from a <see cref="string"/>.
        /// The string must be a valid hexadecimal string of length <see cref="Length"/> * 2 without the '0x' prefix.
        /// </summary>
        /// <param name="value">The <see cref="string"/> to parse.</param>
        /// <returns>The parsed UInt512.</returns>
        /// <exception cref="FormatException">
        /// The <see cref="string"/> is not a valid hexadecimal string of length <see cref="Length"/> * 2 without the '0x' prefix
        /// or the hexadecimal string is invalid.
        /// </exception>
        public static UInt512 Parse(string value)
        {
            var data = value.AsSpan().TrimStartIgnoreCase("0x");
            if (data.Length != Length * 2)
                throw new FormatException($"Invalid UInt512 string format: expected {Length * 2} hexadecimal characters, but got {data.Length}");
            return new UInt512(data.HexToBytesReversed());
        }


        /// <summary>
        /// Serializes the UInt512 to a <see cref="BinaryWriter"/>.
        /// The serialized data will be 8 ulong values in little-endian, and the first ulong value is the least significant.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> to serialize to.</param>
        public void Serialize(BinaryWriter writer) => writer.Write(GetSpan());


        /// <summary>
        /// Deserializes the UInt512 from a <see cref="MemoryReader"/>.
        /// The serialized data should be 8 ulong values in little-endian, and the first ulong value is the least significant.
        /// </summary>
        /// <param name="reader">The <see cref="MemoryReader"/> to deserialize from.</param>
        public void Deserialize(ref MemoryReader reader)
        {
            var span = _holder.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = reader.ReadUInt64();
            }
        }

        public static implicit operator UInt512(string s) => Parse(s);

        public static implicit operator UInt512(byte[] b) => new(b);

        public static bool operator ==(UInt512? left, UInt512? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(UInt512? left, UInt512? right) => !(left == right);

        public static bool operator >(UInt512 left, UInt512 right) => left.CompareTo(right) > 0;

        public static bool operator >=(UInt512 left, UInt512 right) => left.CompareTo(right) >= 0;

        public static bool operator <(UInt512 left, UInt512 right) => left.CompareTo(right) < 0;

        public static bool operator <=(UInt512 left, UInt512 right) => left.CompareTo(right) <= 0;
    }
}