// Copyright (C) 2015-2025 The Neo Project.
//
// ProtocolSettings.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Extensions.Configuration;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Neo
{
    /// <summary>
    /// Represents the protocol settings of the NEO system.
    /// </summary>
    public record ProtocolSettings
    {
        private static readonly IList<Hardfork> AllHardforks = Enum.GetValues(typeof(Hardfork)).Cast<Hardfork>().ToArray();

        /// <summary>
        /// The magic number of the NEO network.
        /// </summary>
        public uint Network { get; init; }

        /// <summary>
        /// The address version of the NEO system.
        /// </summary>
        public byte AddressVersion { get; init; }

        /// <summary>
        /// The public keys of the standby committee members.
        /// </summary>
        public IReadOnlyList<ECPoint> StandbyCommittee { get; init; }

        /// <summary>
        /// The number of members of the committee in NEO system.
        /// </summary>
        public int CommitteeMembersCount => StandbyCommittee.Count;

        /// <summary>
        /// The number of the validators in NEO system.
        /// </summary>
        public int ValidatorsCount { get; init; }

        /// <summary>
        /// The default seed nodes list.
        /// </summary>
        public string[] SeedList { get; init; }

        /// <summary>
        /// Indicates the time in milliseconds between two blocks. Note that starting from
        /// HF_Echidna block generation time is managed by native Policy contract, hence
        /// use NeoSystemExtensions.GetTimePerBlock extension method instead of direct access
        /// to this property.
        /// </summary>
        public uint MillisecondsPerBlock { get; init; }

        /// <summary>
        /// Indicates the time between two blocks. Note that starting from HF_Echidna block
        /// generation time is managed by native Policy contract, hence use
        /// NeoSystemExtensions.GetTimePerBlock extension method instead of direct access
        /// to this property.
        /// </summary>
        public TimeSpan TimePerBlock => TimeSpan.FromMilliseconds(MillisecondsPerBlock);

        /// <summary>
        /// The maximum increment of the <see cref="Transaction.ValidUntilBlock"/> field.
        /// </summary>
        public uint MaxValidUntilBlockIncrement { get; init; }

        /// <summary>
        /// Indicates the maximum number of transactions that can be contained in a block.
        /// </summary>
        public uint MaxTransactionsPerBlock { get; init; }

        /// <summary>
        /// Indicates the maximum number of transactions that can be contained in the memory pool.
        /// </summary>
        public int MemoryPoolMaxTransactions { get; init; }

        /// <summary>
        /// Indicates the maximum number of blocks that can be traced in the smart contract. Note
        /// that starting from HF_Echidna the maximum number of traceable blocks is managed by
        /// native Policy contract, hence use NeoSystemExtensions.GetMaxTraceableBlocks extension
        /// method instead of direct access to this property.
        /// </summary>
        public uint MaxTraceableBlocks { get; init; }

        /// <summary>
        /// Sets the block height from which a hardfork is activated.
        /// </summary>
        public ImmutableDictionary<Hardfork, uint> Hardforks { get; init; }

        /// <summary>
        /// Indicates the amount of gas to distribute during initialization.
        /// In the unit of datoshi, 1 GAS = 1e8 datoshi
        /// </summary>
        public ulong InitialGasDistribution { get; init; }

        private IReadOnlyList<ECPoint> _standbyValidators;
        /// <summary>
        /// The public keys of the standby validators.
        /// </summary>
        public IReadOnlyList<ECPoint> StandbyValidators => _standbyValidators ??= StandbyCommittee.Take(ValidatorsCount).ToArray();

        /// <summary>
        /// The default protocol settings for NEO MainNet.
        /// </summary>
        public static ProtocolSettings Default { get; } = Custom ?? new ProtocolSettings
        {
            Network = 0u,
            AddressVersion = 0x35,
            StandbyCommittee = Array.Empty<ECPoint>(),
            ValidatorsCount = 0,
            SeedList = Array.Empty<string>(),
            MillisecondsPerBlock = 15000,
            MaxTransactionsPerBlock = 512,
            MaxValidUntilBlockIncrement = 86400000 / 15000,
            MemoryPoolMaxTransactions = 50_000,
            MaxTraceableBlocks = 2_102_400,
            InitialGasDistribution = 52_000_000_00000000,
            Hardforks = EnsureOmmitedHardforks(new Dictionary<Hardfork, uint>()).ToImmutableDictionary()
        };

        public static ProtocolSettings Custom { get; set; }

        /// <summary>
        /// Searches for a file in the given path. If not found, checks in the executable directory.
        /// </summary>
        /// <param name="fileName">The name of the file to search for.</param>
        /// <param name="path">The primary path to search in.</param>
        /// <returns>Full path of the file if found, null otherwise.</returns>
        public static string FindFile(string fileName, string path)
        {
            // Check if the given path is relative
            if (!Path.IsPathRooted(path))
            {
                // Combine with the executable directory if relative
                var executablePath = AppContext.BaseDirectory;
                path = Path.Combine(executablePath, path);
            }

            // Check if file exists in the specified (resolved) path
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            // Check if file exists in the executable directory
            var executableDir = AppContext.BaseDirectory;
            fullPath = Path.Combine(executableDir, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            // File not found in either location
            return null;
        }

        /// <summary>
        /// Loads the <see cref="ProtocolSettings"/> from the specified stream.
        /// </summary>
        /// <param name="stream">The stream of the settings.</param>
        /// <returns>The loaded <see cref="ProtocolSettings"/>.</returns>
        public static ProtocolSettings Load(Stream stream)
        {
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
            var section = config.GetSection("ProtocolConfiguration");
            return Load(section);
        }

        /// <summary>
        /// Loads the <see cref="ProtocolSettings"/> at the specified path.
        /// </summary>
        /// <param name="path">The path of the settings file.</param>
        /// <returns>The loaded <see cref="ProtocolSettings"/>.</returns>
        public static ProtocolSettings Load(string path)
        {
            path = FindFile(path, Environment.CurrentDirectory);

            if (path is null)
            {
                return Default;
            }

            using var stream = File.OpenRead(path);
            return Load(stream);
        }

        /// <summary>
        /// Loads the <see cref="ProtocolSettings"/> with the specified <see cref="IConfigurationSection"/>.
        /// </summary>
        /// <param name="section">The <see cref="IConfigurationSection"/> to be loaded.</param>
        /// <returns>The loaded <see cref="ProtocolSettings"/>.</returns>
        public static ProtocolSettings Load(IConfigurationSection section)
        {
            Custom = new ProtocolSettings
            {
                Network = section.GetValue("Network", Default.Network),
                AddressVersion = section.GetValue("AddressVersion", Default.AddressVersion),
                StandbyCommittee = section.GetSection("StandbyCommittee").Exists()
                    ? section.GetSection("StandbyCommittee").GetChildren().Select(p => ECPoint.Parse(p.Get<string>(), ECCurve.Secp256r1)).ToArray()
                    : Default.StandbyCommittee,
                ValidatorsCount = section.GetValue("ValidatorsCount", Default.ValidatorsCount),
                SeedList = section.GetSection("SeedList").Exists()
                    ? section.GetSection("SeedList").GetChildren().Select(p => p.Get<string>()).ToArray()
                    : Default.SeedList,
                MillisecondsPerBlock = section.GetValue("MillisecondsPerBlock", Default.MillisecondsPerBlock),
                MaxTransactionsPerBlock = section.GetValue("MaxTransactionsPerBlock", Default.MaxTransactionsPerBlock),
                MemoryPoolMaxTransactions = section.GetValue("MemoryPoolMaxTransactions", Default.MemoryPoolMaxTransactions),
                MaxTraceableBlocks = section.GetValue("MaxTraceableBlocks", Default.MaxTraceableBlocks),
                MaxValidUntilBlockIncrement = section.GetValue("MaxValidUntilBlockIncrement", Default.MaxValidUntilBlockIncrement),
                InitialGasDistribution = section.GetValue("InitialGasDistribution", Default.InitialGasDistribution),
                Hardforks = section.GetSection("Hardforks").Exists()
                    ? EnsureOmmitedHardforks(section.GetSection("Hardforks").GetChildren().ToDictionary(p => Enum.Parse<Hardfork>(p.Key, true), p => uint.Parse(p.Value))).ToImmutableDictionary()
                    : Default.Hardforks
            };
            CheckingHardfork(Custom);
            return Custom;
        }

        /// <summary>
        /// Explicitly set the height of all old omitted hardforks to 0 for proper IsHardforkEnabled behaviour.
        /// </summary>
        /// <param name="hardForks">HardForks</param>
        /// <returns>Processed hardfork configuration</returns>
        private static Dictionary<Hardfork, uint> EnsureOmmitedHardforks(Dictionary<Hardfork, uint> hardForks)
        {
            foreach (Hardfork hf in AllHardforks)
            {
                if (!hardForks.ContainsKey(hf))
                {
                    hardForks[hf] = 0;
                }
                else
                {
                    break;
                }
            }

            return hardForks;
        }

        private static void CheckingHardfork(ProtocolSettings settings)
        {
            var allHardforks = Enum.GetValues(typeof(Hardfork)).Cast<Hardfork>().ToList();
            // Check for continuity in configured hardforks
            var sortedHardforks = settings.Hardforks.Keys
                .OrderBy(allHardforks.IndexOf)
                .ToList();

            for (int i = 0; i < sortedHardforks.Count - 1; i++)
            {
                int currentIndex = allHardforks.IndexOf(sortedHardforks[i]);
                int nextIndex = allHardforks.IndexOf(sortedHardforks[i + 1]);

                // If they aren't consecutive, return false.
                if (nextIndex - currentIndex > 1)
                    throw new ArgumentException("Hardfork configuration is not continuous.");
            }
            // Check that block numbers are not higher in earlier hardforks than in later ones
            for (int i = 0; i < sortedHardforks.Count - 1; i++)
            {
                if (settings.Hardforks[sortedHardforks[i]] > settings.Hardforks[sortedHardforks[i + 1]])
                {
                    // This means the block number for the current hardfork is greater than the next one, which should not be allowed.
                    throw new ArgumentException($"The Hardfork configuration for {sortedHardforks[i]} is greater than for {sortedHardforks[i + 1]}");
                }
            }
        }

        /// <summary>
        /// Check if the Hardfork is Enabled
        /// </summary>
        /// <param name="hardfork">Hardfork</param>
        /// <param name="index">Block index</param>
        /// <returns>True if enabled</returns>
        public bool IsHardforkEnabled(Hardfork hardfork, uint index)
        {
            if (Hardforks.TryGetValue(hardfork, out uint height))
            {
                // If the hardfork has a specific height in the configuration, check the block height.
                return index >= height;
            }

            // If the hardfork isn't specified in the configuration, return false.
            return false;
        }
    }
}
