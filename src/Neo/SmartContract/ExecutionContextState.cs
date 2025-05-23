// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionContextState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.VM;
using System;

namespace Neo.SmartContract
{
    /// <summary>
    /// Represents the custom state in <see cref="ExecutionContext"/>.
    /// </summary>
    public class ExecutionContextState
    {
        /// <summary>
        /// The script hash of the current context.
        /// </summary>
        public UInt160 ScriptHash { get; set; }

        /// <summary>
        /// The calling context.
        /// </summary>
        public ExecutionContext CallingContext { get; set; }

        /// <summary>
        /// The script hash of the calling native contract. Used in native contracts only.
        /// </summary>
        internal UInt160 NativeCallingScriptHash { get; set; }

        /// <summary>
        /// The <see cref="ContractState"/> of the current context.
        /// </summary>
        public ContractState Contract { get; set; }

        /// <summary>
        /// The <see cref="SmartContract.CallFlags"/> of the current context.
        /// </summary>
        public CallFlags CallFlags { get; set; } = CallFlags.All;

        [Obsolete("Use SnapshotCache instead")]
        public DataCache Snapshot => SnapshotCache;

        public DataCache SnapshotCache { get; set; }

        public int NotificationCount { get; set; }

        public bool IsDynamicCall { get; set; }
    }
}
