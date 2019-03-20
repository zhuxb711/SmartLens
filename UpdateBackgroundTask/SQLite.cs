using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateBackgroundTask
{
    public sealed class SQLite : IDisposable
    {
        private SqliteConnection OLEDB = new SqliteConnection("Filename=SmartLens_SQLite.db");
        private bool IsDisposed = false;
        public SQLite()
        {
            OLEDB.Open();
        }

        public void SetMD5ValueAsync(IList<KeyValuePair<string, string>> Hash)
        {
            StringBuilder sb = new StringBuilder("Delete From HashTable;");
            foreach (var Command in from Command in Hash
                                    select "Insert Into HashTable Values ('" + Command.Key + "','" + Command.Value + "');")
            {
                sb.Append(Command);
            }
            SqliteCommand SQLCommand = new SqliteCommand(sb.ToString(), OLEDB);
            SQLCommand.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                OLEDB.Dispose();
                OLEDB = null;
            }
            IsDisposed = true;
        }

        ~SQLite()
        {
            Dispose();
        }
    }
}
