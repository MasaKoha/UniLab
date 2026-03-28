using System;

namespace UniLab.Persistence
{
    /// <summary>
    /// Contract for local file-based persistent storage with optional TTL support.
    /// </summary>
    public interface ILocalStorage
    {
        /// <summary>
        /// Serializes and saves data under the specified key. Pass a TTL to auto-expire.
        /// </summary>
        void Save<T>(string key, T data, TimeSpan? ttl = null);

        /// <summary>
        /// Loads and deserializes the data stored under the specified key.
        /// Returns a new instance if the key does not exist or the entry has expired.
        /// </summary>
        T Load<T>(string key) where T : new();

        /// <summary>
        /// Deletes the file associated with the specified key.
        /// </summary>
        void Delete(string key);

        /// <summary>
        /// Returns true if a non-expired entry exists for the specified key.
        /// </summary>
        bool Exists(string key);
    }
}
