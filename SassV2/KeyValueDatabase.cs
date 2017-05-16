using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.IO;

namespace SassV2
{
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

		public async Task Open()
		{
			await _connection.OpenAsync();
			var cmd = new SqliteCommand("CREATE TABLE IF NOT EXISTS 'values' (id INTEGER PRIMARY KEY, key TEXT UNIQUE, value TEXT);", _connection);
			await cmd.ExecuteNonQueryAsync();
		}

		public void InsertObject<T>(string key, T obj)
		{
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

		public void InvalidateObject<T>(string key)
		{
			var cmd = new SqliteCommand("DELETE FROM 'values' WHERE key = :key;", _connection);
			cmd.Parameters.AddWithValue("key", key);
			cmd.ExecuteNonQuery();
		}

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
