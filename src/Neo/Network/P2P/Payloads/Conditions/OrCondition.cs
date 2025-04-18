// Copyright (C) 2015-2025 The Neo Project.
//
// OrCondition.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Array = Neo.VM.Types.Array;

namespace Neo.Network.P2P.Payloads.Conditions
{
    /// <summary>
    /// Represents the condition that any of the conditions meets.
    /// </summary>
    public class OrCondition : WitnessCondition, IEquatable<OrCondition>
    {
        /// <summary>
        /// The expressions of the condition.
        /// </summary>
        public WitnessCondition[] Expressions;

        public override int Size => base.Size + Expressions.GetVarSize();
        public override WitnessConditionType Type => WitnessConditionType.Or;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(OrCondition other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null) return false;
            return
                Type == other.Type &&
                Expressions.SequenceEqual(other.Expressions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return obj is OrCondition oc && Equals(oc);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Expressions);
        }

        protected override void DeserializeWithoutType(ref MemoryReader reader, int maxNestDepth)
        {
            Expressions = DeserializeConditions(ref reader, maxNestDepth);
            if (Expressions.Length == 0) throw new FormatException();
        }

        public override bool Match(ApplicationEngine engine)
        {
            return Expressions.Any(p => p.Match(engine));
        }

        protected override void SerializeWithoutType(BinaryWriter writer)
        {
            writer.Write(Expressions);
        }

        private protected override void ParseJson(JObject json, int maxNestDepth)
        {
            JArray expressions = (JArray)json["expressions"];
            if (expressions.Count > MaxSubitems) throw new FormatException();
            Expressions = expressions.Select(p => FromJson((JObject)p, maxNestDepth - 1)).ToArray();
            if (Expressions.Length == 0) throw new FormatException();
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["expressions"] = Expressions.Select(p => p.ToJson()).ToArray();
            return json;
        }

        public override StackItem ToStackItem(IReferenceCounter referenceCounter)
        {
            var result = (Array)base.ToStackItem(referenceCounter);
            result.Add(new Array(referenceCounter, Expressions.Select(p => p.ToStackItem(referenceCounter))));
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(OrCondition left, OrCondition right)
        {
            if (left is null || right is null)
                return Equals(left, right);

            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(OrCondition left, OrCondition right)
        {
            if (left is null || right is null)
                return !Equals(left, right);

            return !left.Equals(right);
        }
    }
}
