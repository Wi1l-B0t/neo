// Copyright (C) 2015-2025 The Neo Project.
//
// Transaction.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using static Neo.SmartContract.Helper;
using Array = Neo.VM.Types.Array;

namespace Neo.Network.P2P.Payloads
{
    /// <summary>
    /// Represents a transaction.
    /// </summary>
    public class Transaction : IEquatable<Transaction>, IInventory, IInteroperable
    {
        /// <summary>
        /// The maximum size of a transaction.
        /// </summary>
        public const int MaxTransactionSize = 102400;

        /// <summary>
        /// The maximum number of attributes that can be contained within a transaction.
        /// </summary>
        public const int MaxTransactionAttributes = 16;

        private byte version;
        private uint nonce;
        // In the unit of datoshi, 1 datoshi = 1e-8 GAS
        private long sysfee;
        // In the unit of datoshi, 1 datoshi = 1e-8 GAS
        private long netfee;
        private uint validUntilBlock;
        private Signer[] _signers;
        private TransactionAttribute[] attributes;
        private ReadOnlyMemory<byte> script;
        private Witness[] witnesses;

        /// <summary>
        /// The size of a transaction header.
        /// </summary>
        public const int HeaderSize =
            sizeof(byte) +  //Version
            sizeof(uint) +  //Nonce
            sizeof(long) +  //SystemFee
            sizeof(long) +  //NetworkFee
            sizeof(uint);   //ValidUntilBlock

        private Dictionary<Type, TransactionAttribute[]> _attributesCache;
        /// <summary>
        /// The attributes of the transaction.
        /// </summary>
        public TransactionAttribute[] Attributes
        {
            get => attributes;
            set { attributes = value; _attributesCache = null; _hash = null; _size = 0; }
        }

        /// <summary>
        /// The <see cref="NetworkFee"/> for the transaction divided by its <see cref="Size"/>.
        /// </summary>
        public long FeePerByte => NetworkFee / Size;

        private UInt256 _hash = null;

        /// <inheritdoc/>
        public UInt256 Hash
        {
            get
            {
                if (_hash == null)
                {
                    _hash = this.CalculateHash();
                }
                return _hash;
            }
        }

        InventoryType IInventory.InventoryType => InventoryType.TX;

        /// <summary>
        /// The network fee of the transaction.
        /// </summary>
        public long NetworkFee //Distributed to consensus nodes.
        {
            get => netfee;
            set { netfee = value; _hash = null; }
        }

        /// <summary>
        /// The nonce of the transaction.
        /// </summary>
        public uint Nonce
        {
            get => nonce;
            set { nonce = value; _hash = null; }
        }

        /// <summary>
        /// The script of the transaction.
        /// </summary>
        public ReadOnlyMemory<byte> Script
        {
            get => script;
            set { script = value; _hash = null; _size = 0; }
        }

        /// <summary>
        /// The sender is the first signer of the transaction, regardless of its <see cref="WitnessScope"/>.
        /// </summary>
        /// <remarks>Note: The sender will pay the fees of the transaction.</remarks>
        public UInt160 Sender => _signers[0].Account;

        /// <summary>
        /// The signers of the transaction.
        /// </summary>
        public Signer[] Signers
        {
            get => _signers;
            set { _signers = value; _hash = null; _size = 0; }
        }

        private int _size;
        public int Size
        {
            get
            {
                if (_size == 0)
                {
                    _size = HeaderSize +
                        Signers.GetVarSize() +      // Signers
                        Attributes.GetVarSize() +   // Attributes
                        Script.GetVarSize() +       // Script
                        Witnesses.GetVarSize();     // Witnesses
                }
                return _size;
            }
        }

        /// <summary>
        /// The system fee of the transaction.
        /// </summary>
        public long SystemFee //Fee to be burned.
        {
            get => sysfee;
            set { sysfee = value; _hash = null; }
        }

        /// <summary>
        /// Indicates that the transaction is only valid before this block height.
        /// </summary>
        public uint ValidUntilBlock
        {
            get => validUntilBlock;
            set { validUntilBlock = value; _hash = null; }
        }

        /// <summary>
        /// The version of the transaction.
        /// </summary>
        public byte Version
        {
            get => version;
            set { version = value; _hash = null; }
        }

        public Witness[] Witnesses
        {
            get => witnesses;
            set { witnesses = value; _size = 0; }
        }

        void ISerializable.Deserialize(ref MemoryReader reader)
        {
            int startPosition = reader.Position;
            DeserializeUnsigned(ref reader);
            Witnesses = reader.ReadSerializableArray<Witness>(Signers.Length);
            if (Witnesses.Length != Signers.Length) throw new FormatException();
            _size = reader.Position - startPosition;
        }

        private static TransactionAttribute[] DeserializeAttributes(ref MemoryReader reader, int maxCount)
        {
            int count = (int)reader.ReadVarInt((ulong)maxCount);
            TransactionAttribute[] attributes = new TransactionAttribute[count];
            HashSet<TransactionAttributeType> hashset = new();
            for (int i = 0; i < count; i++)
            {
                TransactionAttribute attribute = TransactionAttribute.DeserializeFrom(ref reader);
                if (!attribute.AllowMultiple && !hashset.Add(attribute.Type))
                    throw new FormatException();
                attributes[i] = attribute;
            }
            return attributes;
        }

        private static Signer[] DeserializeSigners(ref MemoryReader reader, int maxCount)
        {
            int count = (int)reader.ReadVarInt((ulong)maxCount);
            if (count == 0) throw new FormatException();
            Signer[] signers = new Signer[count];
            HashSet<UInt160> hashset = new();
            for (int i = 0; i < count; i++)
            {
                Signer signer = reader.ReadSerializable<Signer>();
                if (!hashset.Add(signer.Account)) throw new FormatException();
                signers[i] = signer;
            }
            return signers;
        }

        public void DeserializeUnsigned(ref MemoryReader reader)
        {
            Version = reader.ReadByte();
            if (Version > 0) throw new FormatException($"Invalid version: {Version}.");

            Nonce = reader.ReadUInt32();
            SystemFee = reader.ReadInt64();
            if (SystemFee < 0) throw new FormatException($"Invalid system fee: {SystemFee}.");

            NetworkFee = reader.ReadInt64();
            if (NetworkFee < 0) throw new FormatException($"Invalid network fee: {NetworkFee}.");

            if (SystemFee + NetworkFee < SystemFee)
                throw new FormatException($"Invalid fee: {SystemFee} + {NetworkFee} < {SystemFee}.");

            ValidUntilBlock = reader.ReadUInt32();
            Signers = DeserializeSigners(ref reader, MaxTransactionAttributes);
            Attributes = DeserializeAttributes(ref reader, MaxTransactionAttributes - Signers.Length);
            Script = reader.ReadVarMemory(ushort.MaxValue);
            if (Script.Length == 0) throw new FormatException();
        }

        public bool Equals(Transaction other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Hash.Equals(other.Hash);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Transaction);
        }

        void IInteroperable.FromStackItem(StackItem stackItem)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the attribute of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the attribute.</typeparam>
        /// <returns>The first attribute of this type. Or <see langword="null"/> if there is no attribute of this type.</returns>
        public T GetAttribute<T>() where T : TransactionAttribute
        {
            return GetAttributes<T>().FirstOrDefault();
        }

        /// <summary>
        /// Gets all attributes of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the attributes.</typeparam>
        /// <returns>All the attributes of this type.</returns>
        public IEnumerable<T> GetAttributes<T>() where T : TransactionAttribute
        {
            _attributesCache ??= attributes.GroupBy(p => p.GetType()).ToDictionary(p => p.Key, p => p.ToArray());
            if (_attributesCache.TryGetValue(typeof(T), out var result))
                return result.OfType<T>();
            return Enumerable.Empty<T>();
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        public UInt160[] GetScriptHashesForVerifying(DataCache snapshot)
        {
            return Signers.Select(p => p.Account).ToArray();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            ((IVerifiable)this).SerializeUnsigned(writer);
            writer.Write(Witnesses);
        }

        void IVerifiable.SerializeUnsigned(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Nonce);
            writer.Write(SystemFee);
            writer.Write(NetworkFee);
            writer.Write(ValidUntilBlock);
            writer.Write(Signers);
            writer.Write(Attributes);
            writer.WriteVarBytes(Script.Span);
        }

        /// <summary>
        /// Converts the transaction to a JSON object.
        /// </summary>
        /// <param name="settings">The <see cref="ProtocolSettings"/> used during the conversion.</param>
        /// <returns>The transaction represented by a JSON object.</returns>
        public JObject ToJson(ProtocolSettings settings)
        {
            JObject json = new();
            json["hash"] = Hash.ToString();
            json["size"] = Size;
            json["version"] = Version;
            json["nonce"] = Nonce;
            json["sender"] = Sender.ToAddress(settings.AddressVersion);
            json["sysfee"] = SystemFee.ToString();
            json["netfee"] = NetworkFee.ToString();
            json["validuntilblock"] = ValidUntilBlock;
            json["signers"] = Signers.Select(p => p.ToJson()).ToArray();
            json["attributes"] = Attributes.Select(p => p.ToJson()).ToArray();
            json["script"] = Convert.ToBase64String(Script.Span);
            json["witnesses"] = Witnesses.Select(p => p.ToJson()).ToArray();
            return json;
        }

        /// <summary>
        /// Verifies the transaction.
        /// </summary>
        /// <param name="settings">The <see cref="ProtocolSettings"/> used to verify the transaction.</param>
        /// <param name="snapshot">The snapshot used to verify the transaction.</param>
        /// <param name="context">The <see cref="TransactionVerificationContext"/> used to verify the transaction.</param>
        /// <param name="conflictsList">The list of conflicting <see cref="Transaction"/> those fee should be excluded from sender's overall fee during <see cref="TransactionVerificationContext"/>-based verification in case of sender's match.</param>
        /// <returns>The result of the verification.</returns>
        public VerifyResult Verify(ProtocolSettings settings, DataCache snapshot, TransactionVerificationContext context, IEnumerable<Transaction> conflictsList)
        {
            VerifyResult result = VerifyStateIndependent(settings);
            if (result != VerifyResult.Succeed) return result;
            return VerifyStateDependent(settings, snapshot, context, conflictsList);
        }

        /// <summary>
        /// Verifies the state-dependent part of the transaction.
        /// </summary>
        /// <param name="settings">The <see cref="ProtocolSettings"/> used to verify the transaction.</param>
        /// <param name="snapshot">The snapshot used to verify the transaction.</param>
        /// <param name="context">The <see cref="TransactionVerificationContext"/> used to verify the transaction.</param>
        /// <param name="conflictsList">The list of conflicting <see cref="Transaction"/> those fee should be excluded from sender's overall fee during <see cref="TransactionVerificationContext"/>-based verification in case of sender's match.</param>
        /// <returns>The result of the verification.</returns>
        public virtual VerifyResult VerifyStateDependent(ProtocolSettings settings, DataCache snapshot, TransactionVerificationContext context, IEnumerable<Transaction> conflictsList)
        {
            uint height = NativeContract.Ledger.CurrentIndex(snapshot);
            if (ValidUntilBlock <= height || ValidUntilBlock > height + snapshot.GetMaxValidUntilBlockIncrement(settings))
                return VerifyResult.Expired;
            UInt160[] hashes = GetScriptHashesForVerifying(snapshot);
            foreach (UInt160 hash in hashes)
                if (NativeContract.Policy.IsBlocked(snapshot, hash))
                    return VerifyResult.PolicyFail;
            if (!(context?.CheckTransaction(this, conflictsList, snapshot) ?? true)) return VerifyResult.InsufficientFunds;
            long attributesFee = 0;
            foreach (TransactionAttribute attribute in Attributes)
            {
                if (attribute.Type == TransactionAttributeType.NotaryAssisted && !settings.IsHardforkEnabled(Hardfork.HF_Echidna, height))
                    return VerifyResult.InvalidAttribute;
                if (!attribute.Verify(snapshot, this))
                    return VerifyResult.InvalidAttribute;
                attributesFee += attribute.CalculateNetworkFee(snapshot, this);
            }
            long netFeeDatoshi = NetworkFee - (Size * NativeContract.Policy.GetFeePerByte(snapshot)) - attributesFee;
            if (netFeeDatoshi < 0) return VerifyResult.InsufficientFunds;

            if (netFeeDatoshi > MaxVerificationGas) netFeeDatoshi = MaxVerificationGas;
            uint execFeeFactor = NativeContract.Policy.GetExecFeeFactor(snapshot);
            for (int i = 0; i < hashes.Length; i++)
            {
                if (IsSignatureContract(witnesses[i].VerificationScript.Span) && IsSingleSignatureInvocationScript(witnesses[i].InvocationScript, out var _))
                    netFeeDatoshi -= execFeeFactor * SignatureContractCost();
                else if (IsMultiSigContract(witnesses[i].VerificationScript.Span, out int m, out int n) && IsMultiSignatureInvocationScript(m, witnesses[i].InvocationScript, out var _))
                {
                    netFeeDatoshi -= execFeeFactor * MultiSignatureContractCost(m, n);
                }
                else
                {
                    if (!this.VerifyWitness(settings, snapshot, hashes[i], witnesses[i], netFeeDatoshi, out long fee))
                        return VerifyResult.Invalid;
                    netFeeDatoshi -= fee;
                }
                if (netFeeDatoshi < 0) return VerifyResult.InsufficientFunds;
            }
            return VerifyResult.Succeed;
        }

        /// <summary>
        /// Verifies the state-independent part of the transaction.
        /// </summary>
        /// <param name="settings">The <see cref="ProtocolSettings"/> used to verify the transaction.</param>
        /// <returns>The result of the verification.</returns>
        public virtual VerifyResult VerifyStateIndependent(ProtocolSettings settings)
        {
            if (Size > MaxTransactionSize) return VerifyResult.OverSize;
            try
            {
                _ = new Script(Script, true);
            }
            catch (BadScriptException)
            {
                return VerifyResult.InvalidScript;
            }
            UInt160[] hashes = GetScriptHashesForVerifying(null);
            for (int i = 0; i < hashes.Length; i++)
            {
                var witness = witnesses[i];
                if (IsSignatureContract(witness.VerificationScript.Span) && IsSingleSignatureInvocationScript(witness.InvocationScript, out var signature))
                {
                    if (hashes[i] != witness.ScriptHash) return VerifyResult.Invalid;
                    var pubkey = witness.VerificationScript.Span[2..35];
                    try
                    {
                        if (!Crypto.VerifySignature(this.GetSignData(settings.Network), signature.Span, pubkey, ECCurve.Secp256r1))
                            return VerifyResult.InvalidSignature;
                    }
                    catch
                    {
                        return VerifyResult.Invalid;
                    }
                }
                else if (IsMultiSigContract(witness.VerificationScript.Span, out var m, out ECPoint[] points) && IsMultiSignatureInvocationScript(m, witness.InvocationScript, out var signatures))
                {
                    if (hashes[i] != witness.ScriptHash) return VerifyResult.Invalid;
                    var n = points.Length;
                    var message = this.GetSignData(settings.Network);
                    try
                    {
                        for (int x = 0, y = 0; x < m && y < n;)
                        {
                            if (Crypto.VerifySignature(message, signatures[x].Span, points[y]))
                                x++;
                            y++;
                            if (m - x > n - y)
                                return VerifyResult.InvalidSignature;
                        }
                    }
                    catch
                    {
                        return VerifyResult.Invalid;
                    }
                }
            }
            return VerifyResult.Succeed;
        }

        public StackItem ToStackItem(IReferenceCounter referenceCounter)
        {
            if (_signers == null || _signers.Length == 0) throw new ArgumentException("Sender is not specified in the transaction.");
            return new Array(referenceCounter, new StackItem[]
            {
                // Computed properties
                Hash.ToArray(),

                // Transaction properties
                (int)Version,
                Nonce,
                Sender.ToArray(),
                SystemFee,
                NetworkFee,
                ValidUntilBlock,
                Script,
            });
        }

        private static bool IsMultiSignatureInvocationScript(int m, ReadOnlyMemory<byte> invocationScript,
            [NotNullWhen(true)] out ReadOnlyMemory<byte>[] sigs)
        {
            sigs = null;
            ReadOnlySpan<byte> span = invocationScript.Span;
            int i = 0;
            var signatures = new List<ReadOnlyMemory<byte>>();
            while (i < invocationScript.Length)
            {
                if (span[i++] != (byte)OpCode.PUSHDATA1) return false;
                if (i + 65 > invocationScript.Length) return false;
                if (span[i++] != 64) return false;
                signatures.Add(invocationScript[i..(i + 64)]);
                i += 64;
            }
            if (signatures.Count != m) return false;
            sigs = signatures.ToArray();
            return true;
        }

        private static bool IsSingleSignatureInvocationScript(ReadOnlyMemory<byte> invocationScript,
            [NotNullWhen(true)] out ReadOnlyMemory<byte> sig)
        {
            sig = null;
            if (invocationScript.Length != 66) return false;
            ReadOnlySpan<byte> span = invocationScript.Span;
            if ((span[0] != (byte)OpCode.PUSHDATA1) || (span[1] != 64)) return false;
            sig = invocationScript[2..66];
            return true;
        }
    }
}
