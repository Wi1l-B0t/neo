// Copyright (C) 2015-2025 The Neo Project.
//
// FnDsaPublicKey.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Org.BouncyCastle.Pqc.Crypto.Falcon;

namespace Neo.Cryptography;

public class FnDsaPublicKey
{

    private readonly FalconPublicKeyParameters _publicKey;

    public FnDsaPublicKey(FalconPublicKeyParameters publicKey)
    {
        _publicKey = publicKey;
    }

    /// <summary>
    /// Exports the public key in 896 bytes without format byte(0x09)
    /// </summary>
    public byte[] ExportPublicKey() => _publicKey.GetEncoded();

    public bool Verify(byte[] message, byte[] signature)
    {
        var verifier = new FalconSigner();
        verifier.Init(forSigning: false, _publicKey);

        return verifier.VerifySignature(message, signature);
    }
}