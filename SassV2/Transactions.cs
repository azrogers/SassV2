using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SassV2
{
	[SqliteTable("transactions")]
	public class BankTransaction : DataTable<BankTransaction>
	{
		public string DateFormatted => Timestamp.ToString("D");
		public ulong? ServerId => DB.ServerId;

		public static Task<IEnumerable<BankTransaction>> OpenTransactionsByUser(RelationalDatabase db, ulong user)
		{
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT transactions.* FROM transactions\r\nLEFT JOIN balances ON balances.transaction_id = transactions.id\r\nWHERE IFNULL(balances.settled, 0) == 0 AND creator=:creator\r\nGROUP BY transactions.id\r\nORDER BY transactions.timestamp DESC;");
			sqliteCommand.Parameters.AddWithValue("creator", user.ToString());
			return AllFromCommand(db, sqliteCommand);
		}

		public static Task<IEnumerable<BankTransaction>> ClosedTransactionsByUser(RelationalDatabase db, ulong user)
		{
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT transactions.* FROM transactions\r\nWHERE (\r\n\tSELECT COUNT(*) as count FROM balances\r\n\tWHERE balances.transaction_id = transactions.id AND IFNULL(balances.settled, 0) == 0) \r\n== 0 AND creator=:creator\r\nORDER BY transactions.timestamp DESC;");
			sqliteCommand.Parameters.AddWithValue("creator", user.ToString());
			return AllFromCommand(db, sqliteCommand);
		}

		public static Task<IEnumerable<BankTransaction>> TransactionsByUser(RelationalDatabase db, ulong user)
		{
			SqliteCommand sqliteCommand = db.BuildCommand(string.Format("SELECT `id`, {0} FROM transactions WHERE creator=:creator ORDER BY transactions.timestamp DESC;", DataTable<BankTransaction>.FieldNamesFormatted));
			sqliteCommand.Parameters.AddWithValue("creator", user.ToString());
			return AllFromCommand(db, sqliteCommand);
		}

		public static Task<IEnumerable<BankTransaction>> OpenTransactionsForUser(RelationalDatabase db, ulong user)
		{
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT transactions.* FROM balances\r\nLEFT JOIN transactions ON transactions.id = balances.transaction_id\r\nWHERE IFNULL(balances.settled, 0) == 0 AND balances.discord_id = :user\r\nGROUP BY transactions.id\r\nORDER BY transactions.timestamp DESC;");
			sqliteCommand.Parameters.AddWithValue("user", user.ToString());
			return AllFromCommand(db, sqliteCommand);
		}

		public static Task<IEnumerable<BankTransaction>> TransactionsForUser(RelationalDatabase db, ulong user)
		{
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT transactions.* FROM balances\r\nLEFT JOIN transactions ON transactions.id = balances.transaction_id\r\nWHERE balances.discord_id = :user\r\nGROUP BY transactions.id\r\nORDER BY transactions.timestamp DESC;");
			sqliteCommand.Parameters.AddWithValue("user", user.ToString());
			return AllFromCommand(db, sqliteCommand);
		}

		public BankTransaction(RelationalDatabase db) : base(db)
		{
		}

		public BankTransaction(RelationalDatabase db, long id) : base(db)
		{
			Id = id;
			Load().Wait();
		}

		public async Task<bool> UserCanView(RelationalDatabase db, ulong user)
		{
			if(user == Creator)
			{
				return true;
			}

			SqliteCommand sqliteCommand = db.BuildCommand("SELECT COUNT(*) AS rows FROM transactions\r\nLEFT JOIN balances ON balances.transaction_id = transactions.id\r\nWHERE balances.discord_id = :user_id AND transactions.id = :t_id;");
			sqliteCommand.Parameters.AddWithValue("user_id", user);
			sqliteCommand.Parameters.AddWithValue("t_id", Id.Value);
			SqliteDataReader sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			if(!sqliteDataReader.Read())
			{
				return false;
			}

			return sqliteDataReader.GetInt64(0) > 0L;
		}

		[SqliteField("name", DataType.Text, null)]
		public string Name;

		[SqliteField("notes", DataType.Text, null)]
		public string Notes;

		[SqliteField("creator", DataType.Text, null)]
		public ulong Creator;

		[SqliteField("timestamp", DataType.Integer, null, Version = 2L)]
		public DateTime Timestamp = DateTime.Now;
	}

	[SqliteTable("balances")]
	public class BankBalance : DataTable<BankBalance>
	{
		public static Task<IEnumerable<BankBalance>> AllForTransaction(RelationalDatabase db, BankTransaction transaction)
		{
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT * FROM balances WHERE transaction_id=:id ORDER BY settled;");
			sqliteCommand.Parameters.AddWithValue("id", transaction.Id);
			return AllFromCommand(db, sqliteCommand);
		}

		public BankBalance(RelationalDatabase db, long transactionId, ulong discordId, decimal amount) : base(db)
		{
			TransactionId = transactionId;
			DiscordUserId = discordId;
			Amount = amount;
		}

		public BankBalance(RelationalDatabase db, long id) : base(db)
		{
			Id = new long?(id);
			Load().Wait();
		}

		public BankBalance(RelationalDatabase db) : base(db)
		{
		}

		[SqliteField("transaction_id", DataType.Integer, "transactions(id)")]
		public long TransactionId;

		[SqliteField("discord_id", DataType.Text, null)]
		public ulong DiscordUserId;

		[SqliteField("amount", DataType.Integer, null)]
		public decimal Amount;

		[SqliteField("settled", DataType.Integer, null, Version = 2L)]
		public bool Settled;
	}

	[SqliteTable("balance_records")]
	public class BankBalanceRecord : DataTable<BankBalanceRecord>
	{
		public string DateTimeFormatted => Timestamp.ToString("D");

		public static Task<IEnumerable<BankBalanceRecord>> AllForBalance(RelationalDatabase db, BankBalance bal)
		{
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT * FROM balance_records WHERE balance_id=:id ORDER BY timestamp DESC;");
			sqliteCommand.Parameters.AddWithValue("id", bal.Id);
			return AllFromCommand(db, sqliteCommand);
		}

		public BankBalanceRecord(RelationalDatabase db, long balanceId, decimal amount) : base(db)
		{
			BalanceId = balanceId;
			Amount = amount;
		}

		public BankBalanceRecord(RelationalDatabase db, long id) : base(db)
		{
			Id = new long?(id);
			Load().Wait();
		}

		public BankBalanceRecord(RelationalDatabase db) : base(db)
		{
		}

		[SqliteField("balance_id", DataType.Integer, "balances(id)")]
		public long BalanceId;

		[SqliteField("note", DataType.Text, null)]
		public string Note;

		[SqliteField("amount", DataType.Integer, null)]
		public decimal Amount;

		[SqliteField("timestamp", DataType.Integer, null, Version = 2L)]
		public DateTime Timestamp = DateTime.Now;
	}

	[SqliteTable("bank_transaction_index")]
	public class BankTransactionIndex : DataTable<BankTransactionIndex>
	{
		public static async Task<IEnumerable<ulong>> ServersForUser(RelationalDatabase db, ulong id)
		{
			await CreateTable(db);
			SqliteCommand sqliteCommand = db.BuildCommand("SELECT server_id FROM bank_transaction_index WHERE user_id=:id;");
			sqliteCommand.Parameters.AddWithValue("id", id);
			SqliteDataReader sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			List<ulong> list = new List<ulong>();
			while(sqliteDataReader.Read())
			{
				list.Add(ulong.Parse(sqliteDataReader.GetString(0)));
			}
			return list;
		}

		public static async Task RegisterServer(RelationalDatabase db, ulong server, ulong user)
		{
			await new BankTransactionIndex(db)
			{
				ServerId = server,
				UserId = user
			}.Save();
		}

		public BankTransactionIndex(RelationalDatabase db) : base(db)
		{
		}

		protected override async Task CreateIndexes(RelationalDatabase db, long version)
		{
			await db.BuildAndExecute("CREATE UNIQUE INDEX IF NOT EXISTS server_id_uniq ON bank_transaction_index(server_id, user_id);");
		}

		[SqliteField("server_id", DataType.Text, null)]
		public ulong ServerId;

		[SqliteField("user_id", DataType.Text, null)]
		public ulong UserId;
	}
}
