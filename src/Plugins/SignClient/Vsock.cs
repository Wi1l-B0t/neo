// Copyright (C) 2015-2025 The Neo Project.
//
// Vsock.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Net.Sockets;
using Ookii.VmSockets;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace Neo.Plugins.SignClient
{
    public record VsockAddress(int ContextId, int Port);

    /// <summary>
    /// Grpc adapter for VSock. Only supported on Linux.
    /// This is for the SignClient plugin to connect to the AWS Nitro Enclave.
    /// </summary>
    public class Vsock
    {
        private readonly VSockEndPoint _endpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vsock"/> class.
        /// </summary>
        /// <param name="address">The vsock address.</param>
        public Vsock(VsockAddress address)
        {
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("Vsock is only supported on Linux.");

            _endpoint = new VSockEndPoint(address.ContextId, address.Port);
        }

        internal async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("Vsock is only supported on Linux.");

            var socket = VSock.Create(SocketType.Stream);
            try
            {
                await socket.ConnectAsync(_endpoint, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a Grpc channel for the vsock endpoint.
        /// </summary>
        /// <param name="address">The vsock address.</param>
        /// <param name="serviceConfig">The Grpc service config.</param>
        /// <returns>The Grpc channel.</returns>
        public static GrpcChannel CreateChannel(VsockAddress address, ServiceConfig serviceConfig)
        {
            var vsock = new Vsock(address);
            var socketsHttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = vsock.ConnectAsync
            };

            const string AddressPlaceholder = "http://localhost"; // just a placeholder
            return GrpcChannel.ForAddress(AddressPlaceholder, new GrpcChannelOptions
            {
                HttpHandler = socketsHttpHandler,
                ServiceConfig = serviceConfig
            });
        }
    }
}
