using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
using System.IO;

namespace SassV2
{
	public class RelationalDatabase
	{
		private SqliteConnection _connection;

		public RelationalDatabase(string path)
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
		}

		public SqliteCommand BuildCommand(string query)
		{
			return new SqliteCommand(query, _connection);
		}

		public async Task<int> BuildAndExecute(string query)
		{
			return await new SqliteCommand(query, _connection).ExecuteNonQueryAsync();
		}
	}
}
