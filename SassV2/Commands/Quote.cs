using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Microsoft.Data.Sqlite;

namespace SassV2.Commands
{
	public class QuoteCommand
	{
		private const string CREATE_STATEMENT = @"
			CREATE TABLE IF NOT EXISTS quotes (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				quote TEXT,
				author TEXT,
				source TEXT
			);
		";

		[Command(name: "add quote", desc: "add a quote to SASS", usage: "add quote \"<quote>\" \"<author>\" \"<source>\"", category: "Spam")]
		public async static Task<string> AddQuote(DiscordBot bot, IMessage msg, string args)
		{
			var db = bot.RelDatabase(msg.ServerId());
			await InitializeDatabase(db);
			var parts = Util.SplitQuotedString(args);
			if(parts.Length < 2)
			{
				throw new CommandException("You at least need to provide a quote and an author.");
			}

			var quote = parts[0];
			var author = parts[1];
			var source = "Source Unknown";
			if(parts.Length > 2)
			{
				source = parts[2];
			}

			var cmd = db.BuildCommand("INSERT INTO quotes (quote,author,source) VALUES(:quote,:author,:source); select last_insert_rowid();");
			cmd.Parameters.AddWithValue("quote", quote);
			cmd.Parameters.AddWithValue("author", author);
			cmd.Parameters.AddWithValue("source", source);
			var lastId = (long)(await cmd.ExecuteScalarAsync());

			return "Quote #" + lastId + " added.";
		}

		[Command(name: "quote", desc: "get a quote from SASS", usage: "quote\nquote <id>", category: "Spam")]
		public async static Task<string> GetQuote(DiscordBot bot, IMessage msg, string args)
		{
			await InitializeDatabase(bot.RelDatabase(msg.ServerId()));

			SqliteCommand command;
			long quoteId;
			if(!string.IsNullOrWhiteSpace(args))
			{
				if(!long.TryParse(args, out quoteId))
				{
					throw new CommandException("Invalid quote ID.");
				}

				command = bot.RelDatabase(msg.ServerId()).BuildCommand("SELECT * FROM quotes WHERE id = :id;");
				command.Parameters.AddWithValue("id", quoteId);

			}
			else
			{
				command = bot.RelDatabase(msg.ServerId()).BuildCommand("SELECT * FROM quotes ORDER BY RANDOM() LIMIT 1;");
			}

			var reader = await command.ExecuteReaderAsync();
			if(!reader.HasRows)
			{
				throw new CommandException("No quotes found.");
			}
			reader.Read();

			quoteId = reader.GetInt64(0);
			var quote = reader.GetString(1);
			var author = reader.GetString(2);
			var source = reader.GetString(3);
			var sourceBody = "";
			if(source != "Source Unknown")
			{
				sourceBody = $" ({source})";
			}

			return $"\"{quote}\"\n\t\t- *{author}{sourceBody}, #{quoteId}*";
		}

		[Command(name: "edit quote", desc: "change a part of a quote", usage: "edit quote <id> (body|author|source) \"value\"", category: "Administration")]
		public async static Task<string> EditQuote(DiscordBot bot, IMessage msg, string args)
		{
			if(!(msg.Author as IGuildUser).IsAdmin(bot))
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			await InitializeDatabase(bot.RelDatabase(msg.ServerId()));
			var parts = Util.SplitQuotedString(args);

			if(parts.Length < 3)
			{
				throw new CommandException("You're missing some arguments.");
			}

			var id = parts[0];
			var field = parts[1].ToLower();
			var value = parts[2];

			long quoteId;
			if(!long.TryParse(id, out quoteId))
			{
				throw new CommandException("Invalid quote ID.");
			}

			if(field != "body" && field != "author" && field != "source")
			{
				throw new CommandException("Field must be body, author, or source.");
			}

			if(field == "body")
			{
				field = "quote";
			}

			var cmd = bot.RelDatabase(msg.ServerId()).BuildCommand($"UPDATE quotes SET {field} = :value WHERE id = :id;");
			cmd.Parameters.AddWithValue("field", field);
			cmd.Parameters.AddWithValue("value", value);
			cmd.Parameters.AddWithValue("id", quoteId);

			await cmd.ExecuteNonQueryAsync();
			return "ok";
		}

		[Command(name: "quotes", desc: "list all quotes", usage: "quotes", category: "Spam")]
		public static string ListQuotes(DiscordBot bot, IMessage msg, string args)
		{
			return bot.Config.URL + "quotes/" + (msg.Channel as IGuildChannel).GuildId;
		}

		public async static Task InitializeDatabase(RelationalDatabase db)
		{
			var cmd = db.BuildCommand(CREATE_STATEMENT);
			await cmd.ExecuteNonQueryAsync();
		}
	}
}
