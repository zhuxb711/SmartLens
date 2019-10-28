using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace UpdateBackgroundTask
{
    public sealed class SQLite : IDisposable
    {
        private SqliteConnection OLEDB = new SqliteConnection("Filename=SmartLens_SQLite.db");
        private bool IsDisposed = false;
        public SQLite()
        {
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_win32_set_directory(1, ApplicationData.Current.LocalFolder.Path);
            SQLitePCL.raw.sqlite3_win32_set_directory(2, ApplicationData.Current.TemporaryFolder.Path);

            OLEDB.Open();
        }

        public void SetHeshValueAsync(IList<KeyValuePair<string, string>> Hash)
        {
            SqliteTransaction Transaction = OLEDB.BeginTransaction();
            try
            {
                StringBuilder sb = new StringBuilder("Delete From HashTable;");
                foreach (var Command in from Command in Hash
                                        select "Insert Into HashTable Values ('" + Command.Key + "','" + Command.Value + "');")
                {
                    sb.Append(Command);
                }
                using (SqliteCommand SQLCommand = new SqliteCommand(sb.ToString(), OLEDB, Transaction))
                {
                    SQLCommand.ExecuteNonQuery();
                }
                Transaction.Commit();
            }
            catch (Exception)
            {
                Transaction.Rollback();
            }
            finally
            {
                Transaction.Dispose();
            }
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
