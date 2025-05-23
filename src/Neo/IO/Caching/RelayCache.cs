// Copyright (C) 2015-2025 The Neo Project.
//
// RelayCache.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;

namespace Neo.IO.Caching
{
    internal class RelayCache(int maxCapacity) : FIFOCache<UInt256, IInventory>(maxCapacity)
    {
        protected override UInt256 GetKeyForItem(IInventory item)
        {
            return item.Hash;
        }
    }
}

#nullable disable
