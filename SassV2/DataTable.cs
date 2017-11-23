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
		protected static string FieldNamesFormatted
		{
			get
			{
				return string.Join(", ", from f in _fields
										 select "`" + f.GetCustomAttribute<SqliteFieldAttribute>().FieldName + "`");
			}
		}

		protected RelationalDatabase DB
		{
			get
			{
				return _db;
			}
		}

		public long? Id;
		private static HashSet<RelationalDatabase> _createdDbs = new HashSet<RelationalDatabase>();
		private static FieldInfo[] _fields;
		private static Dictionary<string, FieldInfo> _fieldNameToInfo = new Dictionary<string, FieldInfo>();
		private static Logger _logger = LogManager.GetCurrentClassLogger();
		private RelationalDatabase _db;
		protected static string TableName;

		// Token: 0x060000BB RID: 187 RVA: 0x00004C84 File Offset: 0x00002E84
		static DataTable()
		{
			_fields = (from f in typeof(T).GetTypeInfo().GetFields()
					   where f.IsPublic && f.GetCustomAttribute<SqliteFieldAttribute>() != null
					   select f).ToArray<FieldInfo>();
			foreach(FieldInfo fieldInfo in _fields)
			{
				SqliteFieldAttribute customAttribute = fieldInfo.GetCustomAttribute<SqliteFieldAttribute>();
				_fieldNameToInfo[customAttribute.FieldName] = fieldInfo;
			}
			DataTable<T>.TableName = typeof(T).GetTypeInfo().GetCustomAttribute<SqliteTableAttribute>().TableName;
		}

		// Token: 0x060000BC RID: 188 RVA: 0x00004D30 File Offset: 0x00002F30
		public static async Task<T> TryLoad(RelationalDatabase db, long id)
		{
			DataTable<T> obj = (DataTable<T>)Activator.CreateInstance(typeof(T), new object[]
			{
				db
			});
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

		// Token: 0x060000BD RID: 189 RVA: 0x00004D80 File Offset: 0x00002F80
		public static Task<IEnumerable<T>> All(RelationalDatabase db)
		{
			SqliteCommand sqliteCommand = db.BuildCommand(string.Format("SELECT `id`, {0} FROM {1};", DataTable<T>.FieldNamesFormatted, DataTable<T>.TableName));
			DataTable<T>._logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
			return DataTable<T>.AllFromCommand(db, sqliteCommand);
		}

		// Token: 0x060000BE RID: 190 RVA: 0x00004DCC File Offset: 0x00002FCC
		protected static async Task<IEnumerable<T>> AllFromCommand(RelationalDatabase db, SqliteCommand cmd)
		{
			if(!_createdDbs.Contains(db))
			{
				await CreateTable(db);
			}
			List<T> list = new List<T>();
			SqliteDataReader sqliteDataReader = await cmd.ExecuteReaderAsync();
			while(sqliteDataReader.Read())
			{
				object obj = typeof(T).GetTypeInfo().GetConstructor(new Type[]
				{
					typeof(RelationalDatabase)
				}).Invoke(new object[]
				{
					db
				});
				((DataTable<T>)obj).LoadFromReader(sqliteDataReader);
				list.Add((T)obj);
			}
			return list;
		}

		// Token: 0x060000BF RID: 191 RVA: 0x00004E19 File Offset: 0x00003019
		public DataTable(RelationalDatabase db)
		{
			_db = db;
			if(!DataTable<T>._createdDbs.Contains(db))
			{
				DataTable<T>.CreateTable(db).Wait();
			}
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x00004E40 File Offset: 0x00003040
		public virtual async Task Save()
		{
			if(Id != null)
			{
				SqliteCommand sqliteCommand = _db.BuildCommand("");
				List<string> list = new List<string>();
				foreach(FieldInfo fieldInfo in _fields)
				{
					SqliteFieldAttribute customAttribute = fieldInfo.GetCustomAttribute<SqliteFieldAttribute>();
					list.Add(string.Format("{0}=:{1}", customAttribute.FieldName, customAttribute.FieldName));
					Type fieldType = fieldInfo.FieldType;
					object obj = ConvertToSQL(fieldInfo.GetValue(this), fieldType, customAttribute.Type);
					sqliteCommand.Parameters.AddWithValue(customAttribute.FieldName, obj);
				}
				sqliteCommand.CommandText = string.Format("UPDATE {0} SET {1} WHERE id=:id", DataTable<T>.TableName, string.Join(", ", list));
				DataTable<T>._logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
				sqliteCommand.Parameters.AddWithValue("id", Id.Value);
				await sqliteCommand.ExecuteNonQueryAsync();
			}
			else
			{
				List<string> list2 = new List<string>();
				SqliteCommand sqliteCommand2 = _db.BuildCommand("");
				foreach(FieldInfo fieldInfo2 in _fields)
				{
					SqliteFieldAttribute customAttribute2 = fieldInfo2.GetCustomAttribute<SqliteFieldAttribute>();
					list2.Add(":" + customAttribute2.FieldName);
					Type fieldType2 = fieldInfo2.FieldType;
					object obj2 = ConvertToSQL(fieldInfo2.GetValue(this), fieldType2, customAttribute2.Type);
					sqliteCommand2.Parameters.AddWithValue(customAttribute2.FieldName, obj2);
				}
				sqliteCommand2.CommandText = string.Format("INSERT OR IGNORE INTO {0} ({1}) VALUES ({2}); SELECT last_insert_rowid();", DataTable<T>.TableName, DataTable<T>.FieldNamesFormatted, string.Join(", ", list2));
				DataTable<T>._logger.Debug("executing sql statement: " + sqliteCommand2.CommandText);
				SqliteDataReader sqliteDataReader = await sqliteCommand2.ExecuteReaderAsync();
				if(!sqliteDataReader.Read())
				{
					throw new Exception("Insert failed.");
				}
				Id = new long?(sqliteDataReader.GetInt64(0));
			}
		}

		// Token: 0x060000C1 RID: 193 RVA: 0x00004E88 File Offset: 0x00003088
		protected void LoadFromReader(SqliteDataReader reader)
		{
			for(int i = 0; i < reader.FieldCount; i++)
			{
				string name = reader.GetName(i);
				if(name == "id")
				{
					Id = new long?(reader.GetInt64(i));
				}
				else if(_fieldNameToInfo.ContainsKey(name))
				{
					FieldInfo fieldInfo = _fieldNameToInfo[name];
					Type fieldType = reader.GetFieldType(i);
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

		// Token: 0x060000C2 RID: 194 RVA: 0x00004FA4 File Offset: 0x000031A4
		protected async Task Load()
		{
			if(Id == null)
			{
				throw new Exception("No ID for type.");
			}
			SqliteCommand sqliteCommand = _db.BuildCommand(string.Format("SELECT {0} FROM {1} WHERE id=:id;", DataTable<T>.FieldNamesFormatted, DataTable<T>.TableName));
			sqliteCommand.Parameters.AddWithValue("id", Id.Value);
			DataTable<T>._logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
			SqliteDataReader sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			if(!sqliteDataReader.Read())
			{
				throw new NotFoundException("Data not found.");
			}
			LoadFromReader(sqliteDataReader);
		}

		// Token: 0x060000C3 RID: 195 RVA: 0x00004FEC File Offset: 0x000031EC
		protected virtual async Task CreateIndexes(RelationalDatabase db, long version)
		{
			await Task.FromResult(0);
		}

		// Token: 0x060000C4 RID: 196 RVA: 0x0000502C File Offset: 0x0000322C
		protected static async Task CreateTable(RelationalDatabase db)
		{
			if(!_createdDbs.Contains(db))
			{
				var maxVersion = _fields.Select((f) => f.GetCustomAttribute<SqliteFieldAttribute>().Version).Max();
				var version = await DataMigration.GetTableVersion(db, TableName);
				if(version.HasValue && version.Value < maxVersion)
				{
					var newColumns = _fields
						.Select(f => f.GetCustomAttribute<SqliteFieldAttribute>())
						.Where(f => f.Version > version.Value);

					// create new columns
					await db.BuildAndExecute("SAVEPOINT migrate_point;");
					foreach(var sqliteFieldAttribute in newColumns)
					{
						string text = string.Format("ALTER TABLE {0} ADD COLUMN {1} {2}", DataTable<T>.TableName, sqliteFieldAttribute.FieldName, sqliteFieldAttribute.Type.ToString().ToUpper());
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
							indexes.Add(string.Format("FOREIGN KEY({0}) REFERENCES {1}", customAttribute.FieldName, customAttribute.ForeignKeyRelationship));
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

	public class DataMigration
	{
		public static async Task<int?> GetTableVersion(RelationalDatabase db, string table)
		{
			if(!DataMigration._createdDbs.Contains(db))
			{
				await DataMigration.CreateTable(db);
			}
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT MAX(version) FROM migrations WHERE table_name=:name;");
			sqliteCommand.Parameters.AddWithValue("name", table);
			DataMigration._logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
			SqliteDataReader sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			int? result;
			if(!sqliteDataReader.Read() || sqliteDataReader.GetValue(0).GetType() == typeof(DBNull))
			{
				result = null;
			}
			else
			{
				result = new int?(sqliteDataReader.GetInt32(0));
			}
			return result;
		}

		public static async Task UpdateVersion(RelationalDatabase db, string table, long newVersion)
		{
			if(!DataMigration._createdDbs.Contains(db))
			{
				await DataMigration.CreateTable(db);
			}
			SqliteCommand sqliteCommand = db.BuildCommand("INSERT INTO migrations VALUES(:name, :version);");
			sqliteCommand.Parameters.AddWithValue("name", table);
			sqliteCommand.Parameters.AddWithValue("version", newVersion);
			DataMigration._logger.Debug("executing sql statement: " + sqliteCommand.CommandText);
			await sqliteCommand.ExecuteNonQueryAsync();
		}

		private static async Task CreateTable(RelationalDatabase db)
		{
			await db.BuildAndExecute("CREATE TABLE IF NOT EXISTS migrations (\r\n\t\t\t\ttable_name TEXT,\r\n\t\t\t\tversion INTEGER);");
			DataMigration._createdDbs.Add(db);
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
