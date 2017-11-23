using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SassV2
{
	/// <summary>
	/// A key-value database offers a simple key-value store backed by SQLite.
	/// </summary>
	public class KeyValueDatabase
	{
		private SqliteConnection _connection;

		public KeyValueDatabase(string path)
		{
			_connection = new SqliteConnection(new SqliteConnectionStringBuilder()
			{
				DataSource = path
			}.ToString());
		}

		/// <summary>
		/// Initializes this key-value database.
		/// </summary>
		public async Task Open()
		{
			await _connection.OpenAsync();
			var cmd = new SqliteCommand("CREATE TABLE IF NOT EXISTS 'values' (id INTEGER PRIMARY KEY, key TEXT UNIQUE, value TEXT);", _connection);
			await cmd.ExecuteNonQueryAsync();
		}

		/// <summary>
		/// Inserts a given value into the database.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="key">The key to put the value under.</param>
		/// <param name="obj">The object to insert.</param>
		public void InsertObject<T>(string key, T obj)
		{
			// serialize value to json before inserting
			var valueJson = JsonConvert.SerializeObject(obj);
			var cmd = new SqliteCommand("SELECT * FROM 'values' WHERE key = :k;", _connection);
			cmd.Parameters.AddWithValue("k", key);
			var reader = cmd.ExecuteReader();

			if(reader.HasRows)
			{
				var updateCmd = new SqliteCommand("UPDATE 'values' SET value = :v WHERE key = :k;", _connection);
				updateCmd.Parameters.AddWithValue("v", valueJson);
				updateCmd.Parameters.AddWithValue("k", key);
				updateCmd.ExecuteNonQuery();
			}
			else
			{
				var insertCmd = new SqliteCommand("INSERT INTO 'values' (key,value) VALUES (:k,:v);", _connection);
				insertCmd.Parameters.AddWithValue("k", key);
				insertCmd.Parameters.AddWithValue("v", valueJson);
				insertCmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Returns the value under the given key, or default(T) if not found.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="key">The key whose value will be retrieved.</param>
		/// <returns>The value or default(T).</returns>
		public T GetObject<T>(string key)
		{
			var cmd = new SqliteCommand("SELECT value FROM 'values' WHERE key = :key;", _connection);
			cmd.Parameters.AddWithValue("key", key);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
			{
				return default(T);
			}
			reader.Read();

			return JsonConvert.DeserializeObject<T>(reader.GetString(0));
		}

		/// <summary>
		/// Returns the value under the given key, or if it doesn't exist, returns the value of calling createFunc.
		/// DOESN'T INSERT ANYTHING!
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="key">The key whose value to retrieve.</param>
		/// <param name="createFunc">The function that will be called if the key doesn't exist.</param>
		/// <returns>The value at the key or the return value of createFunc.</returns>
		public T GetOrCreateObject<T>(string key, Func<T> createFunc)
		{
			var cmd = new SqliteCommand("SELECT value FROM 'values' WHERE key = :key;", _connection);
			cmd.Parameters.AddWithValue("key", key);
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
			{
				return createFunc();
			}
			reader.Read();
			return JsonConvert.DeserializeObject<T>(reader.GetString(0));
		}

		/// <summary>
		/// Deletes the given key from the database.
		/// </summary>
		/// <param name="key">The key to delete.</param>
		public void InvalidateObject(string key)
		{
			var cmd = new SqliteCommand("DELETE FROM 'values' WHERE key = :key;", _connection);
			cmd.Parameters.AddWithValue("key", key);
			cmd.ExecuteNonQuery();
		}

		/// <summary>
		/// Get keys under a given "namespace" (i.e. ns:key)
		/// </summary>
		/// <typeparam name="T">The type of the given keys to be returned.</typeparam>
		/// <param name="ns">The namespace to look under.</param>
		/// <returns>The keys and values under this namespace.</returns>
		public IEnumerable<KeyValuePair<string, T>> GetKeysOfNamespace<T>(string ns)
		{
			var cmd = new SqliteCommand("SELECT key, value FROM 'values' WHERE key LIKE :ns", _connection);
			cmd.Parameters.AddWithValue("ns", ns + ":%");
			var reader = cmd.ExecuteReader();
			if(!reader.HasRows)
			{
				yield break;
			}

			while(reader.Read())
			{
				var key = reader.GetString(0);
				var value = JsonConvert.DeserializeObject<T>(reader.GetString(1));
				yield return new KeyValuePair<string, T>(key, value);
			}

			yield break;
		}
	}
}
