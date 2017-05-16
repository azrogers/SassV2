using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using SassV2.Web;

namespace SassV2.Commands
{
	public class Bio
	{
		private static List<BioField> _fields = new List<BioField>()
		{
			new BioField("real_name", "Real Name"),
			new BioField("email", "Email"),
			new BioField("paypal", "PayPal Email"),
			new BioField("steam_id", "Steam ID", "Steam", "Find it at <a href='http://steamid.co/' target='_blank'>steamid.co</a>. Use the Steam 64 ID.") {
				Formatter = (v) => $"https://steamcommunity.com/id/{v}"
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
			new BioField("minecraft", "Minecraft Username")
		};
		private static bool _tablesCreated = false;

		public static List<BioField> Fields => _fields;

		[Command("edit bio", Description = "edit your bio", Usage = "edit bio", Category = "Bio")]
		public static async Task<string> EditBio(DiscordBot bot, IMessage msg, string args)
		{
			await CreateTables(bot.GlobalDatabase);
			var message = await AuthCodeManager.GetURL("/bio/edit", msg.Author, bot);
			var channel = await msg.Author.CreateDMChannelAsync();
			await channel.SendMessageAsync(message);
			return null;
		}

		[Command("edit bio", Description = "edit your bio", Usage = "edit bio", Category = "Bio", IsPM = true)]
		public static async Task<string> EditBioPM(DiscordBot bot, IMessage msg, string args)
		{
			await EditBio(bot, msg, args);
			return null;
		}

		[Command("bio", Description = "see a user's bio", Usage = "bio <user>", Category = "bio")]
		public static async Task<string> ShowBio(DiscordBot bot, IMessage msg, string args)
		{
			var users = await Util.FindWithName(args, msg);
			if(!users.Any())
			{
				throw new CommandException("Whose bio do you want me to get?");
			}

			var user = users.First() as IGuildUser;
			var bio = await GetBio(bot, msg.ServerId(), user.Id, bot.GlobalDatabase);
			if(bio == null)
			{
				throw new CommandException("There is no bio for " + user.NicknameOrDefault() + ".");
			}
			
			return bio.GetBioString(user);
		}

		[Command("find", Description = "find a user with a bio entry", Usage = "find <key> <value>", Category = "Bio")]
		public static async Task<string> FindBio(DiscordBot bot, IMessage msg, string args)
		{
			var parts = args.Split(' ');
			if(parts.Length < 2)
			{
				throw new CommandException("You need to specify a key and a value to search for.");
			}

			var key = parts[0];
			var value = string.Join(", ", parts.Skip(1));
			if(!_fields.Where(f => f.Name == key).Any())
			{
				throw new CommandException("Invalid key! Possible values are: " + string.Join(", ", _fields.Select(f => f.Name)) + ".");
			}

			var findCmd = bot.GlobalDatabase.BuildCommand(@"SELECT bios.id FROM bios 
				INNER JOIN bio_privacy ON bio_privacy.bio = bios.id
				INNER JOIN bio_entries ON bio_entries.bio = bios.id
				WHERE bio_entries.key = :key AND bio_entries.value LIKE :search and bio_privacy.server = :server;");
			findCmd.Parameters.AddWithValue("key", key);
			findCmd.Parameters.AddWithValue("search", "%" + value + "%");
			findCmd.Parameters.AddWithValue("server", msg.ServerId().ToString());
			var reader = await findCmd.ExecuteReaderAsync();

			if(reader.HasRows)
			{
				var message = "Users that match your criteria:\n";
				while(reader.Read())
				{
					var id = reader.GetInt64(0);

					var bio = await GetBio(bot, id, bot.GlobalDatabase, false);
					var name = (await (msg.Author as IGuildUser).Guild.GetUserAsync(bio.Owner)).NicknameOrDefault();
					message += "\t" + name + "\n";
				}

				return message.Trim();
			}

			return "Could not find any users by those search terms.";
		}

		public static async Task SaveBio(BioData bio, RelationalDatabase db)
		{
			var fields = bio.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value));

			// find fields that we won't be touching and delete any entries of them for this bio
			var fieldsNotUpdating = _fields.Where(f => !fields.Any(f2 => f2.Name == f.Name));
			foreach(var field in fieldsNotUpdating)
			{
				var delCmd = db.BuildCommand("DELETE FROM bio_entries WHERE key = :key AND bio = :bio");
				delCmd.Parameters.AddWithValue("key", field.Name);
				delCmd.Parameters.AddWithValue("bio", bio.Id);
				await delCmd.ExecuteNonQueryAsync();
			}

			// insert or update fields we do have
			foreach(var field in fields)
			{
				await SaveBioField(bio.Id, field, db);
			}

			// delete all previous server privacy settings
			var clearServerCmd = db.BuildCommand("DELETE FROM bio_privacy WHERE bio = :bio;");
			clearServerCmd.Parameters.AddWithValue("bio", bio.Id);
			await clearServerCmd.ExecuteNonQueryAsync();

			// save new ones
			foreach(var server in bio.SharedServers.Select(k => k.Key))
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
			if(fieldId.HasValue)
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
			if(!reader.HasRows)
			{
				return new List<BioData>();
			}

			var bios = new List<BioData>();
			while(reader.Read())
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
				var guild = await bot.Client.GetGuildAsync(ulong.Parse(serverId));
				if (guild != null)
				{
					servers.Add(new KeyValuePair<ulong, string>(guild.Id, guild.Name));
				}
			}

			var fieldSpecs = new List<BioField>(_fields);
			var fields = new List<BioField>();
			if(full)
			{
				var fieldCmd = db.BuildCommand("SELECT key, value FROM bio_entries WHERE bio=:bio;");
				fieldCmd.Parameters.AddWithValue("bio", id);
				var reader = await fieldCmd.ExecuteReaderAsync();
				while(reader.Read())
				{
					var key = reader.GetString(0);
					var value = reader.GetString(1);
					var spec = fieldSpecs.Where(f => f.Name == key).First();
					if (spec == null)
						continue;
					fieldSpecs.Remove(spec);
					spec.Value = value;
					fields.Add(spec);
				}

				fields.AddRange(fieldSpecs);
			}

			var ownerCmd = db.BuildCommand("SELECT author FROM bios WHERE id=:bio;");
			ownerCmd.Parameters.AddWithValue("bio", id);
			var author = ulong.Parse((string)await ownerCmd.ExecuteScalarAsync());

			return new BioData {
				Id = id,
				Owner = author,
				SharedServers = servers,
				SharedServerNames = servers.Select(s => s.Value).ToList(),
				Fields = fields };
		}

		private static async Task CreateTables(RelationalDatabase db)
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

			public BioField(string name)
				: this(name, name, name, "")
			{ }

			public BioField(string name, string friendlyName)
				:this(name, friendlyName, friendlyName, "")
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
}
