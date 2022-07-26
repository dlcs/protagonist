using System.Collections.Generic;
using System.Linq;

namespace DLCS.Core.Collections
{
    /// <summary>
    /// Class that maintains a 2-way mapping of values.
    /// </summary>
    /// <remarks>All keys+values must be unique.</remarks>
    public class ReadOnlyMap<T1, T2>
    {
        /// <summary>
        /// Create new ReadOnlyMap directly, using mapping function to create reverse lookup.
        /// </summary>
        /// <param name="dictionary">Source dictionary to use.</param>
        /// <param name="ignoreDuplicateValues">
        /// If true, duplicate values in source will be handled by using the first value
        /// </param>
        public ReadOnlyMap(Dictionary<T1, T2> dictionary, bool ignoreDuplicateValues = false)
        {
            Forward = new Indexer<T1, T2>(dictionary);
            
            var reverse = GetReverse(dictionary, ignoreDuplicateValues);
            Reverse = new Indexer<T2, T1>(reverse);
        }

        public Indexer<T1, T2> Forward { get; }
        
        public Indexer<T2, T1> Reverse { get; }

        public class Indexer<T3, T4>
        {
            private readonly Dictionary<T3, T4> dictionary;
            
            public Indexer(Dictionary<T3, T4> dictionary)
            {
                this.dictionary = dictionary;
            }
            
            public T4 this[T3 index] => dictionary[index];
            
            /// <summary>
            /// Gets the value associated with specified key.
            /// </summary>
            public bool TryGetValue(T3 key, out T4? value) => dictionary.TryGetValue(key, out value);
        }
        
        private static Dictionary<T2, T1> GetReverse(Dictionary<T1, T2> dictionary, bool ignoreDuplicateValues)
        {
            if (!ignoreDuplicateValues)
            {
                return dictionary.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            }

            var reverse = new Dictionary<T2, T1>();
            foreach (var (key, value) in dictionary)
            {
                if (!reverse.ContainsKey(value))
                    reverse.Add(value, key);
            }

            return reverse;
        }
    }
}