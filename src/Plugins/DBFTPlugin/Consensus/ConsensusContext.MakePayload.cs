// Copyright (C) 2015-2025 The Neo Project.
//
// ConsensusContext.MakePayload.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.DBFTPlugin.Messages;
using Neo.Plugins.DBFTPlugin.Types;
using Neo.Sign;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Neo.Plugins.DBFTPlugin.Consensus
{
    partial class ConsensusContext
    {
        public ExtensiblePayload MakeChangeView(ChangeViewReason reason)
        {
            return ChangeViewPayloads[MyIndex] = MakeSignedPayload(new ChangeView
            {
                Reason = reason,
                Timestamp = TimeProvider.Current.UtcNow.ToTimestampMS()
            });
        }

        public ExtensiblePayload MakeCommit()
        {
            if (CommitPayloads[MyIndex] is not null)
                return CommitPayloads[MyIndex];

            var block = EnsureHeader();
            CommitPayloads[MyIndex] = MakeSignedPayload(new Commit
            {
                Signature = _signer.SignBlock(block, _myPublicKey, dbftSettings.Network)
            });
            return CommitPayloads[MyIndex];
        }

        private ExtensiblePayload MakeSignedPayload(ConsensusMessage message)
        {
            message.BlockIndex = Block.Index;
            message.ValidatorIndex = (byte)MyIndex;
            message.ViewNumber = ViewNumber;
            ExtensiblePayload payload = CreatePayload(message, null);
            SignPayload(payload);
            return payload;
        }

        private void SignPayload(ExtensiblePayload payload)
        {
            try
            {
                payload.Witness = _signer.SignExtensiblePayload(payload, Snapshot, dbftSettings.Network);
            }
            catch (InvalidOperationException ex)
            {
                Utility.Log(nameof(ConsensusContext), LogLevel.Debug, ex.ToString());
                return;
            }
        }

        /// <summary>
        /// Prevent that block exceed the max size
        /// </summary>
        /// <param name="txs">Ordered transactions</param>
        internal void EnsureMaxBlockLimitation(Transaction[] txs)
        {
            var hashes = new List<UInt256>();
            Transactions = new Dictionary<UInt256, Transaction>();
            VerificationContext = new TransactionVerificationContext();

            // Expected block size
            var blockSize = GetExpectedBlockSizeWithoutTransactions(txs.Length);
            var blockSystemFee = 0L;

            // Iterate transaction until reach the size or maximum system fee
            foreach (Transaction tx in txs)
            {
                // Check if maximum block size has been already exceeded with the current selected set
                blockSize += tx.Size;
                if (blockSize > dbftSettings.MaxBlockSize) break;

                // Check if maximum block system fee has been already exceeded with the current selected set
                blockSystemFee += tx.SystemFee;
                if (blockSystemFee > dbftSettings.MaxBlockSystemFee) break;

                hashes.Add(tx.Hash);
                Transactions.Add(tx.Hash, tx);
                VerificationContext.AddTransaction(tx);
            }

            TransactionHashes = hashes.ToArray();
        }

        public ExtensiblePayload MakePrepareRequest()
        {
            var maxTransactionsPerBlock = neoSystem.Settings.MaxTransactionsPerBlock;
            // Limit Speaker proposal to the limit `MaxTransactionsPerBlock` or all available transactions of the mempool
            EnsureMaxBlockLimitation(neoSystem.MemPool.GetSortedVerifiedTransactions((int)maxTransactionsPerBlock));
            Block.Header.Timestamp = Math.Max(TimeProvider.Current.UtcNow.ToTimestampMS(), PrevHeader.Timestamp + 1);
            Block.Header.Nonce = GetNonce();
            return PreparationPayloads[MyIndex] = MakeSignedPayload(new PrepareRequest
            {
                Version = Block.Version,
                PrevHash = Block.PrevHash,
                Timestamp = Block.Timestamp,
                Nonce = Block.Nonce,
                TransactionHashes = TransactionHashes
            });
        }

        public ExtensiblePayload MakeRecoveryRequest()
        {
            return MakeSignedPayload(new RecoveryRequest
            {
                Timestamp = TimeProvider.Current.UtcNow.ToTimestampMS()
            });
        }

        public ExtensiblePayload MakeRecoveryMessage()
        {
            PrepareRequest prepareRequestMessage = null;
            if (TransactionHashes != null)
            {
                prepareRequestMessage = new PrepareRequest
                {
                    Version = Block.Version,
                    PrevHash = Block.PrevHash,
                    ViewNumber = ViewNumber,
                    Timestamp = Block.Timestamp,
                    Nonce = Block.Nonce,
                    BlockIndex = Block.Index,
                    ValidatorIndex = Block.PrimaryIndex,
                    TransactionHashes = TransactionHashes
                };
            }
            return MakeSignedPayload(new RecoveryMessage
            {
                ChangeViewMessages = LastChangeViewPayloads.Where(p => p != null)
                    .Select(p => GetChangeViewPayloadCompact(p))
                    .Take(M)
                    .ToDictionary(p => p.ValidatorIndex),
                PrepareRequestMessage = prepareRequestMessage,
                // We only need a PreparationHash set if we don't have the PrepareRequest information.
                PreparationHash = TransactionHashes == null
                    ? PreparationPayloads.Where(p => p != null)
                        .GroupBy(p => GetMessage<PrepareResponse>(p).PreparationHash, (k, g) => new { Hash = k, Count = g.Count() })
                        .OrderByDescending(p => p.Count)
                        .Select(p => p.Hash)
                        .FirstOrDefault()
                    : null,
                PreparationMessages = PreparationPayloads.Where(p => p != null)
                    .Select(p => GetPreparationPayloadCompact(p))
                    .ToDictionary(p => p.ValidatorIndex),
                CommitMessages = CommitSent
                    ? CommitPayloads.Where(p => p != null).Select(p => GetCommitPayloadCompact(p)).ToDictionary(p => p.ValidatorIndex)
                    : new Dictionary<byte, RecoveryMessage.CommitPayloadCompact>()
            });
        }

        public ExtensiblePayload MakePrepareResponse()
        {
            return PreparationPayloads[MyIndex] = MakeSignedPayload(new PrepareResponse
            {
                PreparationHash = PreparationPayloads[Block.PrimaryIndex].Hash
            });
        }

        // Related to issue https://github.com/neo-project/neo/issues/3431
        // Ref. https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator?view=net-8.0
        //
        //The System.Random class relies on a seed value that can be predictable,
        //especially if the seed is based on the system clock or other low-entropy sources.
        //RandomNumberGenerator, however, uses sources of entropy provided by the operating
        //system, which are designed to be unpredictable.
        private static ulong GetNonce()
        {
            Span<byte> buffer = stackalloc byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }
    }
}
