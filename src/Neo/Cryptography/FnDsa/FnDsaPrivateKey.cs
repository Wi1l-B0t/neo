// Copyright (C) 2015-2025 The Neo Project.
//
// FnDsaPrivateKey.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Org.BouncyCastle.Pqc.Crypto.Falcon;
using Org.BouncyCastle.Security;

namespace Neo.Cryptography;

public class FnDsaPrivateKey
{

    private readonly FalconPrivateKeyParameters _privateKey;
    private readonly FalconPublicKeyParameters _publicKey;

    private FnDsaPrivateKey(FalconPrivateKeyParameters privateKey, FalconPublicKeyParameters publicKey)
    {
        _privateKey = privateKey;
        _publicKey = publicKey;
    }

    public FnDsaPublicKey PublicKey => new FnDsaPublicKey(_publicKey);

    /// <summary>
    /// Exports the private key without format byte(0x59), 1280 bytes = f + g + F.
    /// Reference: https://openquantumsafe.org/liboqs/algorithms/sig/falcon.html
    /// </summary>
    public byte[] ExportPrivateKey() => _privateKey.GetEncoded();

    public static FnDsaPrivateKey CreateFnDsa512()
    {
        var generator = new FalconKeyPairGenerator();
        generator.Init(new FalconKeyGenerationParameters(new SecureRandom(), FalconParameters.falcon_512));
        var keyPair = generator.GenerateKeyPair();
        return new FnDsaPrivateKey((FalconPrivateKeyParameters)keyPair.Private, (FalconPublicKeyParameters)keyPair.Public);
    }

    /// <summary>
    /// Signs a message using the private key. The signature size is not fixed.
    /// </summary>
    /// <param name="message">The message to be signed.</param>
    /// <returns>The signature as a byte array.</returns>
    public byte[] Sign(byte[] message)
    {
        var signer = new FalconSigner();
        signer.Init(forSigning: true, _privateKey);
        return signer.GenerateSignature(message);
    }
}
