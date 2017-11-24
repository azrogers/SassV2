using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SassV2
{
	public class DataTable<T>
	{
		/// <summary>
		/// Returns the field names for this table formatted for use in a SQL statement.
		/// </summary>
		protected static string FieldNamesFormatted => 
			string.Join(", ", _fields.Select(f =>  "`" + f.GetCustomAttribute<SqliteFieldAttribute>().FieldName + "`"));

		/// <summary>
		/// Returns the DB behind this DataTable.
		/// </summary>
		protected RelationalDatabase DB => _db;

		/// <summary>
		/// Returns the ID of this object.
		/// </summary>
		public long? Id;

		/// <summary>
		/// Returns the name of this SQLite table.
		/// </summary>
		protected static string TableName;

		private static HashSet<RelationalDatabase> _createdDbs = new HashSet<RelationalDatabase>();
		private static FieldInfo[] _fields;
		private static Dictionary<string, FieldInfo> _fieldNameToInfo = new Dictionary<string, FieldInfo>();
		private static Logger _logger = LogManager.GetCurrentClassLogger();
		private RelationalDatabase _db;
		
		static DataTable()
		{
			_fields = typeof(T).GetTypeInfo().GetFields()
				.Where(f => f.IsPublic && f.GetCustomAttribute<SqliteFieldAttribute>() != null).ToArray();

			foreach(FieldInfo fieldInfo in _fields)
			{
				var customAttribute = fieldInfo.GetCustomAttribute<SqliteFieldAttribute>();
				_fieldNameToInfo[customAttribute.FieldName] = fieldInfo;
			}

			TableName = typeof(T).GetTypeInfo().GetCustomAttribute<SqliteTableAttribute>().TableName;
		}
		
		/// <summary>
		/// Loads an object with this ID if it exists.
		/// </summary>
		public static async Task<T> TryLoad(RelationalDatabase db, long id)
		{
			DataTable<T> obj = (DataTable<T>)Activator.CreateInstance(typeof(T), new object[] { db });
			obj.Id = new long?(id);
			try
			{
				await obj.Load();
			}
			catch(NotFoundException)
			{
				return default(T);
			}
			return (T)((object)obj);
		}
		
		/// <summary>
		/// Returns all objects of this type.
		/// </summary>
		public static Task<IEnumerable<T>> All(RelationalDatabase db)
		{
			var sqliteCommand = db.BuildCommand(string.Format("SELECT `id`, {0} FROM {1};", FieldNamesFormatted, TableName));
			_logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
			return AllFromCommand(db, sqliteCommand);
		}
		
		/// <summary>
		/// Returns all results from this command.
		/// </summary>
		protected static async Task<IEnumerable<T>> AllFromCommand(RelationalDatabase db, SqliteCommand cmd)
		{
			if(!_createdDbs.Contains(db))
			{
				await CreateTable(db);
			}

			_logger.Debug("executing sql statement: " + cmd.CommandText);

			var list = new List<T>();
			var sqliteDataReader = await cmd.ExecuteReaderAsync();
			while(sqliteDataReader.Read())
			{
				object obj = typeof(T).GetTypeInfo().GetConstructor(new Type[]
				{
					typeof(RelationalDatabase)
				}).Invoke(new object[] { db });
				((DataTable<T>)obj).LoadFromReader(sqliteDataReader);
				list.Add((T)obj);
			}

			return list;
		}
		
		public DataTable(RelationalDatabase db)
		{
			_db = db;
			if(!_createdDbs.Contains(db))
			{
				CreateTable(db).Wait();
			}
		}
		
		/// <summary>
		/// Saves this object.
		/// </summary>
		public virtual async Task Save()
		{
			// if we've created this object yet
			if(Id != null)
			{
				var sqliteCommand = _db.BuildCommand("");
				var list = new List<string>();
				// create update statement
				foreach(var fieldInfo in _fields)
				{
					var customAttribute = fieldInfo.GetCustomAttribute<SqliteFieldAttribute>();
					list.Add(string.Format("{0}=:{1}", customAttribute.FieldName, customAttribute.FieldName));
					var fieldType = fieldInfo.FieldType;
					var obj = ConvertToSQL(fieldInfo.GetValue(this), fieldType, customAttribute.Type);
					sqliteCommand.Parameters.AddWithValue(customAttribute.FieldName, obj);
				}

				sqliteCommand.CommandText = string.Format("UPDATE {0} SET {1} WHERE id=:id", DataTable<T>.TableName, string.Join(", ", list));
				_logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
				sqliteCommand.Parameters.AddWithValue("id", Id.Value);
				await sqliteCommand.ExecuteNonQueryAsync();
			}
			else // insert it
			{
				var insertFields = new List<string>();
				var sqliteCommand2 = _db.BuildCommand("");
				foreach(var fieldInfo2 in _fields)
				{
					var customAttribute2 = fieldInfo2.GetCustomAttribute<SqliteFieldAttribute>();
					insertFields.Add(":" + customAttribute2.FieldName);
					var fieldType2 = fieldInfo2.FieldType;
					var obj2 = ConvertToSQL(fieldInfo2.GetValue(this), fieldType2, customAttribute2.Type);
					sqliteCommand2.Parameters.AddWithValue(customAttribute2.FieldName, obj2);
				}

				sqliteCommand2.CommandText = $"INSERT OR IGNORE INTO {TableName} ({FieldNamesFormatted}) VALUES ({string.Join(", ", insertFields)}); SELECT last_insert_rowid();";
				_logger.Debug("executing sql statement: " + sqliteCommand2.CommandText);
				var sqliteDataReader = await sqliteCommand2.ExecuteReaderAsync();
				if(!sqliteDataReader.Read())
				{
					throw new Exception("Insert failed.");
				}

				Id = sqliteDataReader.GetInt64(0);
			}
		}
		
		/// <summary>
		/// Loads this class from the given SqliteDataReader.
		/// </summary>
		protected void LoadFromReader(SqliteDataReader reader)
		{
			for(int i = 0; i < reader.FieldCount; i++)
			{
				string name = reader.GetName(i);
				if(name == "id")
				{
					Id = reader.GetInt64(i);
				}
				else if(_fieldNameToInfo.ContainsKey(name))
				{
					var fieldInfo = _fieldNameToInfo[name];
					var fieldType = reader.GetFieldType(i);
					if(fieldType == typeof(long))
					{
						fieldInfo.SetValue(this, ConvertFromSQL(reader.GetInt64(i), fieldInfo.FieldType, DataType.Integer));
					}
					else if(fieldType == typeof(double))
					{
						fieldInfo.SetValue(this, ConvertFromSQL(reader.GetDouble(i), fieldInfo.FieldType, DataType.Real));
					}
					else if(fieldType == typeof(string))
					{
						fieldInfo.SetValue(this, ConvertFromSQL(reader.GetString(i), fieldInfo.FieldType, DataType.Text));
					}
					else
					{
						if(fieldType == typeof(byte[]))
						{
							throw new Exception("Blob type not supported.");
						}
						fieldInfo.SetValue(this, null);
					}
				}
			}
		}
		
		/// <summary>
		/// Load this table, if its ID is set.
		/// </summary>
		protected async Task Load()
		{
			if(Id == null)
			{
				throw new Exception("No ID for type.");
			}

			var sqliteCommand = _db.BuildCommand(string.Format("SELECT {0} FROM {1} WHERE id=:id;", FieldNamesFormatted, TableName));
			sqliteCommand.Parameters.AddWithValue("id", Id.Value);
			_logger.Debug("executing sql statement: " + sqliteCommand.CommandText);

			var sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			if(!sqliteDataReader.Read())
			{
				throw new NotFoundException("Data not found.");
			}
			LoadFromReader(sqliteDataReader);
		}
		
		/// <summary>
		/// Creates the indexes for this table. Should be overwritten if your class has indexes.
		/// </summary>
		protected virtual async Task CreateIndexes(RelationalDatabase db, long version)
		{
			await Task.FromResult(0);
		}
		
		/// <summary>
		/// Creates the table for this class.
		/// </summary>
		protected static async Task CreateTable(RelationalDatabase db)
		{
			if(_createdDbs.Contains(db))
				return;

			var tableExistsCmd = db.BuildCommand($"SELECT name FROM sqlite_master WHERE type='table' AND name='{TableName}';");
			var tableExists = (await tableExistsCmd.ExecuteReaderAsync()).HasRows;
			var maxVersion = _fields.Select((f) => f.GetCustomAttribute<SqliteFieldAttribute>().Version).Max();
			var version = await DataMigration.GetTableVersion(db, TableName);
			// guess it existed before we had a migration for it...
			if(!version.HasValue && tableExists)
				version = 1;
			if(version.HasValue && version.Value < maxVersion)
			{
				var newColumns = _fields
					.Select(f => f.GetCustomAttribute<SqliteFieldAttribute>())
					.Where(f => f.Version > version.Value);

				// create new columns
				await db.BuildAndExecute("SAVEPOINT migrate_point;");
				foreach(var sqliteFieldAttribute in newColumns)
				{
					string text = $"ALTER TABLE {TableName} ADD COLUMN {sqliteFieldAttribute.FieldName} {sqliteFieldAttribute.Type.ToString().ToUpper()}";
					_logger.Debug("executing sql statement: " + text);
					await db.BuildAndExecute(text);
				}

				// update migration version
				await db.BuildAndExecute("RELEASE migrate_point;");
				await DataMigration.UpdateVersion(db, TableName, maxVersion);
				_createdDbs.Add(db);

				// create indexes on new object
				await ((DataTable<T>)Activator.CreateInstance(typeof(T), new object[]
				{
					db
				})).CreateIndexes(db, maxVersion);
			}
			else
			{
				var fieldText = new List<string>
				{
					"id INTEGER PRIMARY KEY"
				};
				var indexes = new List<string>();
				var fields = _fields;
				for(int i = 0; i < fields.Length; i++)
				{
					var customAttribute = fields[i].GetCustomAttribute<SqliteFieldAttribute>();
					fieldText.Add(customAttribute.FieldName + " " + customAttribute.Type.ToString().ToUpper());
					if(customAttribute.ForeignKeyRelationship != null)
					{
						indexes.Add($"FOREIGN KEY({customAttribute.FieldName}) REFERENCES {customAttribute.ForeignKeyRelationship}");
					}
					if(customAttribute.Version > maxVersion)
					{
						maxVersion = customAttribute.Version;
					}
				}

				// create table
				var indexTxt = (indexes.Count > 0 ? (", " + string.Join(", ", indexes)) : "");
				var createStmt = $"CREATE TABLE IF NOT EXISTS {TableName} ({string.Join(", ", fieldText)}{indexTxt});";
				_logger.Debug("executing sql statement: " + createStmt);
				await db.BuildAndExecute(createStmt);
				await DataMigration.UpdateVersion(db, TableName, maxVersion);
				_createdDbs.Add(db);

				// create indexes
				await ((DataTable<T>)Activator.CreateInstance(typeof(T), new object[]
				{
					db
				})).CreateIndexes(db, maxVersion);
			}
		}

		private object ConvertToSQL(object val, Type fromType, DataType sqlType)
		{
			if(fromType == typeof(decimal) && sqlType == DataType.Integer)
			{
				return (long)Math.Floor((decimal)val * 100m);
			}
			if(fromType == typeof(bool) && sqlType == DataType.Integer)
			{
				return ((bool)val) ? 1 : 0;
			}
			if(fromType == typeof(DateTime) && sqlType == DataType.Integer)
			{
				return ((DateTime)val).ToUnixTime();
			}
			return val;
		}

		private object ConvertFromSQL(object val, Type toType, DataType sqlType)
		{
			if(toType == typeof(decimal) && sqlType == DataType.Integer)
			{
				return (long)val / 100m;
			}
			if(toType == typeof(ulong) && sqlType == DataType.Text)
			{
				return ulong.Parse((string)val);
			}
			if(toType == typeof(bool) && sqlType == DataType.Integer)
			{
				return (long)val > 0L;
			}
			if(toType != typeof(DateTime) || sqlType != DataType.Integer)
			{
				return val;
			}
			if((long)val < 0L)
			{
				return Util.FromUnixTime(0L);
			}
			return Util.FromUnixTime((long)val);
		}
	}

	/// <summary>
	/// Handles table migrations.
	/// </summary>
	public class DataMigration
	{
		public static async Task<int?> GetTableVersion(RelationalDatabase db, string table)
		{
			if(!_createdDbs.Contains(db))
			{
				await CreateTable(db);
			}

			SqliteCommand sqliteCommand = db.BuildCommand("SELECT MAX(version) FROM migrations WHERE table_name=:name;");
			sqliteCommand.Parameters.AddWithValue("name", table);
			_logger.Debug("executing sql statement: " + sqliteCommand.CommandText);

			SqliteDataReader sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			if(!sqliteDataReader.Read() || sqliteDataReader.GetValue(0).GetType() == typeof(DBNull))
			{
				return null;
			}

			return sqliteDataReader.GetInt32(0);
		}

		public static async Task UpdateVersion(RelationalDatabase db, string table, long newVersion)
		{
			if(!_createdDbs.Contains(db))
			{
				await CreateTable(db);
			}

			SqliteCommand sqliteCommand = db.BuildCommand("INSERT INTO migrations VALUES(:name, :version);");
			sqliteCommand.Parameters.AddWithValue("name", table);
			sqliteCommand.Parameters.AddWithValue("version", newVersion);
			_logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
			await sqliteCommand.ExecuteNonQueryAsync();
		}

		private static async Task CreateTable(RelationalDatabase db)
		{
			await db.BuildAndExecute("CREATE TABLE IF NOT EXISTS migrations (\r\n\t\t\t\ttable_name TEXT,\r\n\t\t\t\tversion INTEGER);");
			_createdDbs.Add(db);
		}

		private static HashSet<RelationalDatabase> _createdDbs = new HashSet<RelationalDatabase>();
		private static Logger _logger = LogManager.GetCurrentClassLogger();
	}

	public enum DataType
	{
		Text,
		Integer,
		Real,
		Blob
	}

	public class SqliteFieldAttribute : Attribute
	{
		public SqliteFieldAttribute(string name, DataType type, string fk = null)
		{
			FieldName = name;
			Type = type;
			ForeignKeyRelationship = fk;
		}

		public string FieldName;
		public DataType Type;
		public string ForeignKeyRelationship;
		public long Version = 1L;
	}

	public class SqliteTableAttribute : Attribute
	{
		public SqliteTableAttribute(string name)
		{
			TableName = name;
		}

		public string TableName;
	}

	public class NotFoundException : Exception
	{
		public NotFoundException(string msg) : base(msg)
		{
		}
	}
}
