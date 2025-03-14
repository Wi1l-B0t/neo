// Copyright (C) 2015-2025 The Neo Project.
//
// TransactionRemovalReason.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Ledger
{
    /// <summary>
    /// The reason a transaction was removed.
    /// </summary>
    public enum TransactionRemovalReason : byte
    {
        /// <summary>
        /// The transaction was rejected since it was the lowest priority transaction and the memory pool capacity was exceeded.
        /// </summary>
        CapacityExceeded,

        /// <summary>
        /// The transaction was rejected due to failing re-validation after a block was persisted.
        /// </summary>
        NoLongerValid,

        /// <summary>
        /// The transaction was rejected due to conflict with higher priority transactions with Conflicts attribute.
        /// </summary>
        Conflict,
    }
}
