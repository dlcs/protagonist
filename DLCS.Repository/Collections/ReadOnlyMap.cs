using System.Collections.Generic;
using System.Linq;

namespace DLCS.Repository.Collections
{
    /// <summary>
    /// Class that maintains a 2-way mapping of values.
    /// </summary>
    /// <remarks>All keys+values must be unique.</remarks>
    public class ReadOnlyMap<T1, T2>
    {
        public ReadOnlyMap(Dictionary<T1, T2> dictionary)
        {
            Forward = new Indexer<T1, T2>(dictionary);
            
            var reverse = dictionary.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
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
        }
    }
}