using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Data.Sqlite;

namespace SassV2.Commands
{
	public class QuoteCommand : ModuleBase<SocketCommandContext>
	{
		private const string CREATE_STATEMENT = @"
			CREATE TABLE IF NOT EXISTS quotes (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				quote TEXT,
				author TEXT,
				source TEXT
			);
		";

		private DiscordBot _bot;

		public QuoteCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "add quote", 
			desc: "Adds a quote to SASS.", 
			usage: "add quote \"<quote>\" \"<author>\" \"<source>\"", 
			example: "add quote \"Mess with the best, die like the rest.\" \"Dade Murphy\" \"Hackers\"",
			category: "Quote")]
		[Command("add quote")]
		[RequireContext(ContextType.Guild)]
		public async Task AddQuote([Remainder] string args)
		{
			var db = _bot.RelDatabase(Context.Guild.Id);
			await InitializeDatabase(db);
			var parts = Util.SplitQuotedString(args);
			if(parts.Length < 2)
			{
				await ReplyAsync("You at least need to provide a quote and an author.");
				return;
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

			await ReplyAsync("Quote #" + lastId + " added.");
		}

		[SassCommand(
			name: "quote", 
			desc: "Get a quote from SASS.", 
			usage: "quote\nquote <id>", 
			example: "quote 1",
			category: "Quote")]
		[Command("quote")]
		[RequireContext(ContextType.Guild)]
		public async Task GetQuote(long id)
		{
			await InitializeDatabase(_bot.RelDatabase(Context.Guild.Id));

			SqliteCommand command;
			long quoteId;
			if(id == -1)
			{
				command = _bot.RelDatabase(Context.Guild.Id).BuildCommand("SELECT * FROM quotes ORDER BY RANDOM() LIMIT 1;");
			}
			else
			{
				command = _bot.RelDatabase(Context.Guild.Id).BuildCommand("SELECT * FROM quotes WHERE id = :id;");
				command.Parameters.AddWithValue("id", id);
			}

			var reader = await command.ExecuteReaderAsync();
			if(!reader.HasRows)
			{
				await ReplyAsync("No quotes found.");
				return;
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

			await ReplyAsync($"\"{quote}\"\n\t\t- *{author}{sourceBody}, #{quoteId}*");
		}

		[Command("quote")]
		[RequireContext(ContextType.Guild)]
		public async Task GetQuote()
		{
			await GetQuote(-1);
		}

		[SassCommand(
			name: "edit quote", 
			desc: "Change a part of a quote. This requires admin permissions.", 
			usage: "edit quote <id> (body|author|source) \"value\"", 
			example: "edit quote 1 source \"Hackers (1995, Iain Softley)\"",
			category: "Quote")]
		[Command("edit quote")]
		[RequireContext(ContextType.Guild)]
		public async Task EditQuote(long quoteId, string field, [Remainder] string args)
		{
			if(!(Context.User as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync("You're not allowed to use this command.");
				return;
			}

			await InitializeDatabase(_bot.RelDatabase(Context.User.Id));
			
			if(field != "body" && field != "author" && field != "source")
			{
				await ReplyAsync("Field must be body, author, or source.");
				return;
			}

			if(field == "body")
			{
				field = "quote";
			}

			var cmd = _bot.RelDatabase(Context.Guild.Id).BuildCommand($"UPDATE quotes SET {field} = :value WHERE id = :id;");
			cmd.Parameters.AddWithValue("field", field);
			cmd.Parameters.AddWithValue("value", args.Trim('"'));
			cmd.Parameters.AddWithValue("id", quoteId);

			await cmd.ExecuteNonQueryAsync();
			await ReplyAsync("ok");
		}

		[SassCommand(
			name: "quotes", 
			desc: "List all quotes.", 
			usage: "quotes", 
			category: "Quote")]
		[Command("quotes")]
		[RequireContext(ContextType.Guild)]
		public async Task ListQuotes()
		{
			await ReplyAsync(_bot.Config.URL + "quotes/" + Context.Guild.Id);
		}

		public async static Task InitializeDatabase(RelationalDatabase db)
		{
			var cmd = db.BuildCommand(CREATE_STATEMENT);
			await cmd.ExecuteNonQueryAsync();
		}
	}
}
