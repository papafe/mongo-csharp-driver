/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MongoDB.Bson.SourceGeneration.Generator
{
    // Value-equatable wrapper around ImmutableArray<T>. ImmutableArray<T> uses reference
    // equality, which defeats incremental generator caching for any pipeline node that
    // contains a collection. EquatableArray<T> compares element-wise, so two collections
    // with the same items hash and compare equal. See Roslyn incremental generator hygiene
    // notes for why this matters.
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

        private readonly ImmutableArray<T> _array;

        public EquatableArray(ImmutableArray<T> array)
        {
            _array = array;
        }

        public int Count => _array.IsDefault ? 0 : _array.Length;

        public T this[int index] => _array[index];

        public bool Equals(EquatableArray<T> other)
        {
            if (_array.IsDefault) { return other._array.IsDefault; }
            if (other._array.IsDefault) { return false; }
            return _array.AsSpan().SequenceEqual(other._array.AsSpan());
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            if (_array.IsDefaultOrEmpty) { return 0; }
            unchecked
            {
                var hash = 17;
                foreach (var item in _array)
                {
                    hash = (hash * 31) + (item?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        public IEnumerator<T> GetEnumerator() => _array.IsDefault
            ? Enumerable.Empty<T>().GetEnumerator()
            : ((IEnumerable<T>)_array).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
