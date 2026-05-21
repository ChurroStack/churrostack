using System.Runtime.Serialization;

namespace ChurrunKubernetes.Models.Dtos
{
    [Serializable]
    [DataContract]
    public class QueryResult<T>
    {
        /// <summary>
        /// Total items in server
        /// </summary>
        [DataMember]
        public long Count { get; protected set; }

        /// <summary>
        /// Items retrieved
        /// </summary>
        [DataMember]
        public IEnumerable<T> Items { get; protected set; }

        /// <summary>
        /// A continuation token
        /// </summary>
        [DataMember]
        public string? ContinuationToken { get; protected set; }

        public QueryResult(IEnumerable<T> items, long? count = null, string? continuationToken = null)
        {
            Items = items;
            Count = count ?? items.Count();
            ContinuationToken = continuationToken;
        }
    }
}
