using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class QuoteCommand : ModuleBase<SocketCommandContext>
	{
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
			var parts = Util.SplitQuotedString(args);
			if(parts.Length < 2)
			{
				await ReplyAsync("You at least need to provide a quote and an author.");
				return;
			}

			var body = parts[0];
			var author = parts[1];
			var source = "Source Unknown";
			if(parts.Length > 2)
			{
				source = parts[2];
			}

			var quote = new Quote(db)
			{
				Body = body,
				Source = source,
				Author = author
			};
			await quote.Save();

			await ReplyAsync("Quote #" + quote.Id + " added.");
		}

		[SassCommand(
			name: "quote",
			desc: "Get a quote from SASS.",
			usage: "quote\nquote <id>",
			example: "quote 1",
			category: "Quote")]
		[Command("quote")]
		[RequireContext(ContextType.Guild)]
		public async Task GetQuote(long id = -1)
		{
			Quote quote;

			var db = _bot.RelDatabase(Context.Guild.Id);
			if(id == -1)
			{
				quote = await Quote.RandomQuote(db);
			}
			else
			{
				quote = await Quote.TryLoad(db, id);
			}

			if(quote == null)
			{
				await ReplyAsync("No quotes found.");
				return;
			}

			var sourceBody = "";
			if(quote.Source != "Source Unknown")
			{
				sourceBody = $" ({quote.Source})";
			}

			await ReplyAsync($"\"{quote.Body}\"\n\t\t- *{quote.Author}{sourceBody}, #{quote.Id}*");
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

			var db = _bot.RelDatabase(Context.Guild.Id);
			var quote = await Quote.TryLoad(db, quoteId);
			if(quote == null)
			{
				await ReplyAsync("The specified quote doesn't exist.");
				return;
			}

			if(field == "body")
			{
				quote.Body = args.Trim();
			}
			else if(field == "author")
			{
				quote.Author = args.Trim();
			}
			else if(field == "source")
			{
				quote.Source = args.Trim();
			}
			else
			{
				await ReplyAsync("Field must be one of 'body', 'author', or 'source'.");
				return;
			}

			await quote.Save();
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

		[SassCommand("delete quote", "Delete a specific quote.", "delete quote <id>", "Quote")]
		[Command("delete quote")]
		[RequireContext(ContextType.Guild)]
		public async Task DeleteQuote(long quoteId)
		{
			if(!(Context.User as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync("You're not allowed to use this command.");
				return;
			}

			var db = _bot.RelDatabase(Context.Guild.Id);
			await Quote.DeleteQuote(db, quoteId);
			await ReplyAsync("Deleted quote. Moved above quotes down - quote IDs changed.");
		}
	}

	[SqliteTable("quotes")]
	public class Quote : DataTable<Quote>
	{
		[SqliteField("quote", DataType.Text)]
		public string Body;
		[SqliteField("author", DataType.Text)]
		public string Author;
		[SqliteField("source", DataType.Text)]
		public string Source;
		[SqliteField("soft_deleted", DataType.Integer, Version = 2)]
		public bool SoftDeleted = false;

		/// <summary>
		/// Returns a random quote from the database.
		/// </summary>
		public static async Task<Quote> RandomQuote(RelationalDatabase db)
		{
			var cmd = db.BuildCommand("SELECT * FROM quotes WHERE IFNULL(soft_deleted, 0)=0 ORDER BY RANDOM() LIMIT 1;");
			var quotes = await AllFromCommand(db, cmd);
			if(!quotes.Any())
				return null;
			return quotes.First();
		}

		/// <summary>
		/// Delete the specified quote.
		/// </summary>
		public static async Task DeleteQuote(RelationalDatabase db, long id)
		{
			await db.BuildAndExecute("BEGIN TRANSACTION;");

			// do the deletion
			var cmd = db.BuildCommand("DELETE FROM quotes WHERE id = :id;");
			cmd.Parameters.AddWithValue("id", id);
			await cmd.ExecuteNonQueryAsync();

			var findQuoteCmd = db.BuildCommand("SELECT `id` FROM quotes WHERE id > :id;");
			findQuoteCmd.Parameters.AddWithValue("id", id);
			var reader = await findQuoteCmd.ExecuteReaderAsync();

			// go through every quote with an id > this one and bring their IDs down
			long num = id;
			while(reader.Read())
			{
				await db.BuildAndExecute($"UPDATE quotes SET id={num} WHERE id={reader.GetInt64(0)};");
				num += 1;
			}
			await db.BuildAndExecute($"UPDATE SQLITE_SEQUENCE SET seq = {num - 1} WHERE name = 'quotes';");
			// commit
			await db.BuildAndExecute("END TRANSACTION;");
		}

		public Quote(RelationalDatabase db) : base(db)
		{

		}

		public Quote(RelationalDatabase db, long id) : base(db)
		{
			Id = id;
			Load().Wait();
		}
	}
}
