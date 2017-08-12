﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using SassV2.Web;
using Discord.Commands;

namespace SassV2.Commands
{
	public class BioCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public BioCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "edit bio", 
			desc: "Edit your bio. Works via DM too.", 
			usage: "edit bio", 
			category: "Bio")]
		[Command("edit bio")]
		public async Task EditBio()
		{
			await Bio.CreateTables(_bot.GlobalDatabase);
			var message = await AuthCodeManager.GetURL("/bio/edit", Context.User, _bot);
			var channel = await Context.User.CreateDMChannelAsync();
			await channel.SendMessageAsync(message);
		}

		[SassCommand(
			"bio", 
			desc: "see a user's bio", 
			usage: "bio <name or mention>", 
			example: "bio Scripted Automated Speech System",
			category: "Bio")]
		[Command("bio")]
		[RequireContext(ContextType.Guild)]
		public async Task ShowBio([Remainder] string args)
		{
			IEnumerable<IUser> users = await Util.FindWithName(args, Context.Message);

			if (!users.Any())
			{
				await ReplyAsync("I don't know who that is.");
				return;
			}

			var user = users.First() as IGuildUser;
			var bio = await Bio.GetBio(_bot, Context.Guild.Id, user.Id, _bot.GlobalDatabase);
			if(bio == null)
			{
				await ReplyAsync("There is no bio for " + user.NicknameOrDefault() + ".");
				return;
			}

			bio.Fields = bio.Fields.OrderBy(f => Bio.Fields.IndexOf(Bio.Fields.Where(f2 => f2.Name == f.Name).First())).ToList();
			
			await ReplyAsync(bio.GetBioString(user));
		}

		[SassCommand(
			name: "find", 
			desc: "Finds all users whose bio contains this specific value.", 
			usage: "find <key> <value>", 
			example: "find twitter _cpancake",
			category: "Bio")]
		[Command("find")]
		[RequireContext(ContextType.Guild)]
		public async Task FindBio(string key, [Remainder] string value)
		{
			if(!Bio.Fields.Where(f => f.Name == key).Any())
			{
				await ReplyAsync("Invalid key! Possible values are: " + string.Join(", ", Bio.Fields.Select(f => f.Name)) + ".");
				return;
			}

			var findCmd = _bot.GlobalDatabase.BuildCommand(@"SELECT bios.id FROM bios 
				INNER JOIN bio_privacy ON bio_privacy.bio = bios.id
				INNER JOIN bio_entries ON bio_entries.bio = bios.id
				WHERE bio_entries.key = :key AND bio_entries.value LIKE :search and bio_privacy.server = :server;");
			findCmd.Parameters.AddWithValue("key", key);
			findCmd.Parameters.AddWithValue("search", "%" + value + "%");
			findCmd.Parameters.AddWithValue("server", Context.Guild.Id.ToString());
			var reader = await findCmd.ExecuteReaderAsync();

			if (reader.HasRows)
			{
				var message = "Users that match your criteria:\n";
				while (reader.Read())
				{
					var id = reader.GetInt64(0);

					var bio = await Bio.GetBio(_bot, id, _bot.GlobalDatabase, false);
					var name = (await (Context.User as IGuildUser).Guild.GetUserAsync(bio.Owner)).NicknameOrDefault();
					message += "\t" + name + "\n";
				}

				await ReplyAsync(message.Trim());
			}
			else
			{
				await ReplyAsync("Could not find any users by those search terms.");
			}
		}
		
		
	}

	public static class Bio
	{
		public static List<BioField> Fields => _fields;

		private static List<BioField> _fields = new List<BioField>()
		{
			new BioField("real_name", "Real Name"),
			new BioField("bio", "Bio", "Who dis?") { Formatter = (v) => $"```{v}```", Multiline = true, MaxLength = 500 },
			new BioField("email", "Email"),
			new BioField("paypal", "PayPal Email"),
			new BioField("steam_id", "Steam ID", "Steam", "Find it at <a href='http://steamid.co/' target='_blank'>steamid.co</a>. Use the Steam 64 ID.") {
				Formatter = (v) => $"https://steamcommunity.com/profiles/{v}"
			},
			new BioField("twitter", "Twitter Username", "Twitter", "") {
				Formatter = (v) => $"https://twitter.com/{v}"
			},
			new BioField("facebook", "Facebook Username", "Facebook", "The part that comes after the facebook.com/ on your Facebook page.") {
				Formatter = (v) => $"https://facebook.com/{v}"
			},
			new BioField("snapchat", "Snapchat Username"),
			new BioField("battle_net", "Battle.net Username"),
			new BioField("origin", "Origin Username"),
			new BioField("uplay", "UPlay Username"),
			new BioField("psn", "PSN Username"),
			new BioField("xbl", "XBL Username"),
			new BioField("friendcode", "3DS Friend Code") { Formatter = (v) => $"`{v}`" },
			new BioField("switchcode", "Switch Friend Code") { Formatter = (v) => $"`{v}`" },
			new BioField("minecraft", "Minecraft Username")
		};

		private static bool _tablesCreated = false;

		public static async Task SaveBio(BioData bio, RelationalDatabase db)
		{
			var fields = bio.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value));

			// find fields that we won't be touching and delete any entries of them for this bio
			var fieldsNotUpdating = _fields.Where(f => !fields.Any(f2 => f2.Name == f.Name));
			foreach (var field in fieldsNotUpdating)
			{
				var delCmd = db.BuildCommand("DELETE FROM bio_entries WHERE key = :key AND bio = :bio");
				delCmd.Parameters.AddWithValue("key", field.Name);
				delCmd.Parameters.AddWithValue("bio", bio.Id);
				await delCmd.ExecuteNonQueryAsync();
			}

			// insert or update fields we do have
			foreach (var field in fields)
			{
				if (field.Value.Length > field.MaxLength)
				{
					field.Value = field.Value.Substring(0, field.MaxLength);
				}

				await SaveBioField(bio.Id, field, db);
			}

			// delete all previous server privacy settings
			var clearServerCmd = db.BuildCommand("DELETE FROM bio_privacy WHERE bio = :bio;");
			clearServerCmd.Parameters.AddWithValue("bio", bio.Id);
			await clearServerCmd.ExecuteNonQueryAsync();

			// save new ones
			foreach (var server in bio.SharedServers.Select(k => k.Key))
			{
				await SaveServerPrivacy(bio.Id, server, db);
			}
		}

		private static async Task SaveBioField(long bio, BioField field, RelationalDatabase db)
		{
			var existsCmd = db.BuildCommand("SELECT id FROM bio_entries WHERE key = :key AND bio = :bio");
			existsCmd.Parameters.AddWithValue("key", field.Name);
			existsCmd.Parameters.AddWithValue("bio", bio);
			var fieldId = (long?)await existsCmd.ExecuteScalarAsync();
			if (fieldId.HasValue)
			{
				var updateCmd = db.BuildCommand("UPDATE bio_entries SET value = :value WHERE id = :id;");
				updateCmd.Parameters.AddWithValue("value", field.Value);
				updateCmd.Parameters.AddWithValue("id", fieldId.Value);
				await updateCmd.ExecuteNonQueryAsync();
				return;
			}

			var insertCmd = db.BuildCommand("INSERT INTO bio_entries(`bio`, `key`, `value`) VALUES(:bio, :key, :value);");
			insertCmd.Parameters.AddWithValue("bio", bio);
			insertCmd.Parameters.AddWithValue("key", field.Name);
			insertCmd.Parameters.AddWithValue("value", field.Value);
			await insertCmd.ExecuteNonQueryAsync();
		}

		private static async Task SaveServerPrivacy(long bio, ulong server, RelationalDatabase db)
		{
			var serverCmd = db.BuildCommand("INSERT INTO bio_privacy(bio, server) VALUES(:bio, :server);");
			serverCmd.Parameters.AddWithValue("bio", bio);
			serverCmd.Parameters.AddWithValue("server", server.ToString());
			await serverCmd.ExecuteNonQueryAsync();
		}

		public static async Task<long> CreateBio(IUser user, RelationalDatabase db)
		{
			await CreateTables(db);

			var bioCmd = db.BuildCommand("INSERT INTO bios(`author`) VALUES(:author); select last_insert_rowid();");
			bioCmd.Parameters.AddWithValue("author", user.Id.ToString());
			return (long)await bioCmd.ExecuteScalarAsync();
		}

		public static async Task<List<BioData>> GetBios(DiscordBot bot, IUser user, RelationalDatabase db)
		{
			await CreateTables(db);

			var biosCmd = db.BuildCommand("SELECT id FROM bios WHERE author=:author;");
			biosCmd.Parameters.AddWithValue("author", user.Id.ToString());
			var reader = await biosCmd.ExecuteReaderAsync();
			if (!reader.HasRows)
			{
				return new List<BioData>();
			}

			var bios = new List<BioData>();
			while (reader.Read())
			{
				var id = reader.GetInt64(0);

				bios.Add(await GetBio(bot, id, db));
			}

			return bios;
		}

		public static async Task<BioData> GetBio(DiscordBot bot, ulong server, ulong user, RelationalDatabase db)
		{
			await CreateTables(db);

			var bioCmd = db.BuildCommand(@"SELECT bios.id FROM bios 
				INNER JOIN bio_privacy ON bio_privacy.bio = bios.id
				WHERE bios.author = :user and bio_privacy.server = :server;");
			bioCmd.Parameters.AddWithValue("user", user.ToString());
			bioCmd.Parameters.AddWithValue("server", server.ToString());
			var bioId = (long?)await bioCmd.ExecuteScalarAsync();
			if (bioId.HasValue)
				return await GetBio(bot, bioId.Value, db, true);
			return null;
		}

		public static async Task<BioData> GetBio(DiscordBot bot, long id, RelationalDatabase db, bool full = false)
		{
			await CreateTables(db);

			var bioPrivacyCmd = db.BuildCommand("SELECT server FROM bio_privacy WHERE bio=:bio;");
			bioPrivacyCmd.Parameters.AddWithValue("bio", id);
			var reader2 = await bioPrivacyCmd.ExecuteReaderAsync();

			var servers = new List<KeyValuePair<ulong, string>>();
			while (reader2.Read())
			{
				var serverId = reader2.GetString(0);
				var guild = bot.Client.GetGuild(ulong.Parse(serverId));
				if (guild != null)
				{
					servers.Add(new KeyValuePair<ulong, string>(guild.Id, guild.Name));
				}
			}

			var fieldSpecs = new List<BioField>(_fields.Select(f => f.Clone()));
			var fields = new List<BioField>();
			if (full)
			{
				var fieldCmd = db.BuildCommand("SELECT key, value FROM bio_entries WHERE bio=:bio;");
				fieldCmd.Parameters.AddWithValue("bio", id);
				var reader = await fieldCmd.ExecuteReaderAsync();
				while (reader.Read())
				{
					var key = reader.GetString(0);
					var value = reader.GetString(1);
					var spec = fieldSpecs.Where(f => f.Name == key).First();
					if (spec == null)
						continue;
					fieldSpecs.Remove(spec);
					var newField = new BioField(spec.Name, spec.FriendlyName, spec.DisplayName, spec.Info)
					{
						Formatter = spec.Formatter,
						Value = value
					};
					fields.Add(newField);
				}

				fields.AddRange(fieldSpecs);
			}

			var ownerCmd = db.BuildCommand("SELECT author FROM bios WHERE id=:bio;");
			ownerCmd.Parameters.AddWithValue("bio", id);
			var author = ulong.Parse((string)await ownerCmd.ExecuteScalarAsync());

			return new BioData
			{
				Id = id,
				Owner = author,
				SharedServers = servers,
				SharedServerNames = servers.Select(s => s.Value).ToList(),
				Fields = fields
			};
		}

		internal static async Task CreateTables(RelationalDatabase db)
		{
			if (_tablesCreated) return;

			await db.BuildAndExecute(@"CREATE TABLE IF NOT EXISTS bios (id INTEGER PRIMARY KEY, author TEXT);");
			await db.BuildAndExecute("CREATE INDEX IF NOT EXISTS bios_author ON bios(author);");
			await db.BuildAndExecute(@"
				CREATE TABLE IF NOT EXISTS bio_entries(
					id INTEGER PRIMARY KEY,
					bio INTEGER, key TEXT, value TEXT,
					FOREIGN KEY(bio) REFERENCES bios(id));");
			await db.BuildAndExecute("CREATE INDEX IF NOT EXISTS bio_entries_bio ON bio_entries(bio);");
			await db.BuildAndExecute("CREATE INDEX IF NOT EXISTS bio_entries_key ON bio_entries(key);");
			await db.BuildAndExecute(@"CREATE TABLE IF NOT EXISTS bio_privacy(
				id INTEGER PRIMARY KEY, 
				bio INTEGER, server TEXT,
				FOREIGN KEY(bio) REFERENCES bios(id));");
			await db.BuildAndExecute("CREATE INDEX IF NOT EXISTS bio_privacy_bio ON bio_privacy(bio);");
			await db.BuildAndExecute("CREATE INDEX IF NOT EXISTS bio_privacy_server ON bio_privacy(server);");
			_tablesCreated = true;
		}
	}

	public class BioData
	{
		public long Id;
		public ulong Owner;
		public List<KeyValuePair<ulong, string>> SharedServers;
		public List<string> SharedServerNames;
		public List<BioField> Fields;

		public string GetBioString(IGuildUser user)
		{
			var message = $"Bio for {user.NicknameOrDefault()}:\n";
			foreach (var field in Fields)
			{
				if (string.IsNullOrWhiteSpace(field.Value)) continue;
				message += $"\t{field.DisplayName}: {field.Formatter(field.Value)}\n";
			}

			return message.Trim();
		}
	}

	public class BioField
	{
		public string Name;
		public string FriendlyName;
		public string DisplayName;
		public string Info = "";
		public string Value = "";
		public Func<string, string> Formatter;
		public bool Multiline = false;
		public int MaxLength = 100;

		public BioField Clone()
		{
			var newField = new BioField(Name)
			{
				FriendlyName = FriendlyName,
				DisplayName = DisplayName,
				Info = Info,
				Value = Value,
				Formatter = Formatter,
				Multiline = Multiline
			};

			return newField;
		}

		public BioField(string name)
			: this(name, name, name, "")
		{ }

		public BioField(string name, string friendlyName)
			: this(name, friendlyName, friendlyName, "")
		{ }

		public BioField(string name, string friendlyName, string info)
			: this(name, friendlyName, friendlyName, info)
		{ }

		public BioField(string name, string friendlyName, string displayName, string info)
		{
			Name = name;
			FriendlyName = friendlyName;
			DisplayName = displayName;
			Info = info;
			Formatter = (v) => v;
		}
	}
}
