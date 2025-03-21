// Copyright (C) 2015-2025 The Neo Project.
//
// ReflectionCache.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Neo.IO.Caching
{
    internal static class ReflectionCache<T>
        where T : Enum
    {
        private static readonly Dictionary<T, Type> s_dictionary = [];

        public static int Count => s_dictionary.Count;

        static ReflectionCache()
        {
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // Get attribute
                var attribute = field.GetCustomAttribute<ReflectionCacheAttribute>();
                if (attribute == null) continue;

                // Append to cache
                var key = (T?)field.GetValue(null);
                if (key == null) continue;
                s_dictionary.Add(key, attribute.Type);
            }
        }

        public static object? CreateInstance(T key, object? def = null)
        {
            // Get Type from cache
            if (s_dictionary.TryGetValue(key, out var t))
                return Activator.CreateInstance(t);

            // return null
            return def;
        }

        public static ISerializable? CreateSerializable(T key, ReadOnlyMemory<byte> data)
        {
            if (s_dictionary.TryGetValue(key, out var t))
                return data.AsSerializable(t);

            return null;
        }
    }
}

#nullable disable
