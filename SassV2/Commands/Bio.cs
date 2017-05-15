using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Bio
	{
		private static async void CreateTables(RelationalDatabase db)
		{
			await db.BuildAndExecute(@"CREATE TABLE IF NOT EXISTS bios (id INTEGER PRIMARY KEY AUTOINCREMENT, author TEXT);");
			await db.BuildAndExecute("CREATE INDEX bios_author ON bios(author);");
			await db.BuildAndExecute(@"
				CREATE TABLE IF NOT EXISTS bio_entries(
					id INTEGER PRIMARY KEY AUTOINCREMENT,
					bio INTEGER, key TEXT, value TEXT,
					FOREIGN KEY(bio) REFERENCES bios(id));");
			await db.BuildAndExecute("CREATE INDEX bio_entries_bio ON bio_entries(bio);");
			await db.BuildAndExecute("CREATE INDEX bio_entries_key ON bio_entries(key);");
			await db.BuildAndExecute(@"CREATE TABLE IF NOT EXISTS bio_privacy(
				id INTEGER PRIMARY KEY AUTOINCREMENT, 
				bio INTEGER, server TEXT,
				FOREIGN KEY(bio) REFERENCES bios(id));");
			await db.BuildAndExecute("CREATE INDEX bio_privacy_bio ON bio_privacy(bio);");
			await db.BuildAndExecute("CREATE INDEX bio_privacy_server ON bio_privacy(server);");
		}
	}
}
