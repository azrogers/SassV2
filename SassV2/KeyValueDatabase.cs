using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
using Newtonsoft.Json;
using System.IO;

namespace SassV2
{
	public class KeyValueDatabase
	{
		private SqliteConnection _connection;

		public KeyValueDatabase(string path)
		{
			if(!File.Exists(path))
			{
				SqliteConnection.CreateFile(path);
			}
			_connection = new SqliteConnection("Data Source=" + path + "; Version=3;journal_mode=WAL;");
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
			var cmd = new SqliteCommand("SELECT * FROM 'values' WHERE key = ?;", _connection);
			cmd.Parameters.AddWithValue(null, key);
			var reader = cmd.ExecuteReader();
			if(reader.HasRows)
			{
				var updateCmd = new SqliteCommand("UPDATE 'values' SET value = ? WHERE key = ?;", _connection);
				updateCmd.Parameters.AddWithValue(null, valueJson);
				updateCmd.Parameters.AddWithValue(null, key);
				updateCmd.ExecuteNonQuery();
			}
			else
			{
				var insertCmd = new SqliteCommand("INSERT INTO 'values' (key,value) VALUES (?,?);", _connection);
				insertCmd.Parameters.AddWithValue(null, key);
				insertCmd.Parameters.AddWithValue(null, valueJson);
				insertCmd.ExecuteNonQuery();
			}
		}

		public T GetObject<T>(string key)
		{
			var cmd = new SqliteCommand("SELECT value FROM 'values' WHERE key = ?;", _connection);
			cmd.Parameters.AddWithValue(null, key);
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
			var cmd = new SqliteCommand("SELECT value FROM 'values' WHERE key = ?;", _connection);
			cmd.Parameters.AddWithValue(null, key);
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
			var cmd = new SqliteCommand("DELETE FROM 'values' WHERE key = ?;", _connection);
			cmd.Parameters.AddWithValue(null, key);
			cmd.ExecuteNonQuery();
		}

		public IEnumerable<KeyValuePair<string, T>> GetKeysOfNamespace<T>(string ns)
		{
			var cmd = new SqliteCommand("SELECT key, value FROM 'values' WHERE key LIKE ?", _connection);
			cmd.Parameters.AddWithValue(null, ns + ":%");
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
