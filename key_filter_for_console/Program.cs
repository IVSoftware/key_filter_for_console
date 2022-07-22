using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SQLite;
// Markdown resize 
// https://gist.github.com/uupaa/f77d2bcf4dc7a294d109

namespace key_filter_for_console
{
    class Program
    {
        enum ConsoleState
        {
            Menu,
            NewNote,
            SearchNotes,
            RecordsShown,
        }
        static void Main(string[] args)
        {
            SetForegroundWindow(GetConsoleWindow());
            ConsoleApplication.Run().Wait();
        }

        class ConsoleApplication
        {
            private ConsoleApplication()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_connectionString));
                using (var cnx = new SQLiteConnection(_connectionString))
                {
                    cnx.CreateTable<Note>();
                }
                CurrentState = ConsoleState.Menu;
                Task.Run(() => filterKeyboardMessages());
            }
            private static SemaphoreSlim _running = new SemaphoreSlim(0, 1);
            public static SemaphoreSlim Run()
            {
                if (_consoleApplication == null)
                {
                    _consoleApplication = new ConsoleApplication();
                }
                return _running;
            }


            private static ConsoleApplication _consoleApplication = null;
            private static string _connectionString =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "key_filter_for_console",
                    "notes.db");

            private async void filterKeyboardMessages()
            {
                while (true)
                {
                    while (tryGetConsoleKey(out ConsoleKeyInfo keyInfo))
                    {
                        // C O N T R O L
                        if (keyInfo.Modifiers == ConsoleModifiers.Control)
                        {
                            switch (keyInfo.Key)
                            {
                                case ConsoleKey.X:
                                    // EXIT regardless of state
                                    _running.Release();
                                    return;
                            }
                        }
                        switch (CurrentState)
                        {
                            case ConsoleState.Menu:
                                // Only certain keys allowed
                                switch (keyInfo.Key)
                                {
                                    case ConsoleKey.D1:
                                        CurrentState = ConsoleState.NewNote;
                                        break;
                                    case ConsoleKey.D2:
                                        CurrentState = ConsoleState.SearchNotes;
                                        break;
                                    case ConsoleKey.D3:
                                        using (var cnx = new SQLiteConnection(_connectionString))
                                        {
                                            var sql = $"SELECT COUNT(*) FROM notes";
                                            var count = cnx.ExecuteScalar<uint>(sql);
                                            Console.WriteLine();
                                            Console.WriteLine($"Database contains {count} records.");
                                        }
                                        break;
                                    case ConsoleKey.D4:
                                        using (var cnx = new SQLiteConnection(_connectionString))
                                        {
                                            var sql = $"DELETE FROM notes";
                                            cnx.Execute(sql);
                                        }
                                        Console.WriteLine();
                                        Console.WriteLine("Notes cleared.");
                                        break;
                                }
                                break;
                            case ConsoleState.NewNote:
                                switch (keyInfo.Key)
                                {
                                    case ConsoleKey.Escape:
                                        CurrentState = ConsoleState.Menu;
                                        break;
                                }
                                break;
                            case ConsoleState.SearchNotes:
                                switch (keyInfo.Key)
                                {
                                    case ConsoleKey.Escape:
                                        CurrentState = ConsoleState.Menu;
                                        break;
                                    case ConsoleKey.Enter:
                                        var query = _searchTerm.ToString();
                                        if (string.IsNullOrEmpty(query))
                                        {
                                            CurrentState = ConsoleState.Menu;
                                        }
                                        else
                                        {
                                            Console.Clear();
                                            List<Note> recordset;
                                            using (var cnx = new SQLiteConnection(_connectionString))
                                            {
                                                var sql = $"SELECT * FROM notes WHERE Text LIKE '%{query}%'";
                                                recordset = cnx.Query<Note>(sql);
                                            }
                                            Console.WriteLine($"Found {recordset.Count} Notes containing '{query}'.");
                                            foreach (var note in recordset)
                                            {
                                                Console.WriteLine("- - -");
                                                Console.WriteLine(note.Text);
                                            }
                                            Console.WriteLine("==================");
                                            Console.WriteLine("END of notes");
                                            Console.WriteLine("    ESCAPE to return to Main Menu");
                                            Console.WriteLine("    BACKSPACE search again");
                                            Console.WriteLine("    CONTROL + X to exit.");
                                            CurrentState = ConsoleState.RecordsShown;
                                        }
                                        break;
                                    case ConsoleKey.Backspace:
                                        // https://stackoverflow.com/a/24404619/5438626
                                        Console.Write("\b \b");
                                        _searchTerm.RemoveLast();
                                        break;
                                    default:
                                        Console.Write(keyInfo.KeyChar);
                                        _searchTerm.Add($"{keyInfo.KeyChar}");
                                        break;
                                }
                                // Be WAY more responsive when typing is allowed.
                                await Task.Delay(1);
                                continue;
                            case ConsoleState.RecordsShown:
                                switch (keyInfo.Key)
                                {
                                    case ConsoleKey.Escape:
                                        CurrentState = ConsoleState.Menu;
                                        break;
                                    case ConsoleKey.Backspace:
                                        CurrentState = ConsoleState.SearchNotes;
                                        break;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    // In non-typing states, be efficient with core cycles by being less responsive.
                    await Task.Delay(250);
                }
                { }
            }


            private static ConsoleState _CurrentState = (ConsoleState)(-1);
            private static ConsoleState CurrentState
            {
                get => _CurrentState;
                set
                {
                    if (!Equals(_CurrentState, value))
                    {
                        _CurrentState = value;
                        switch (_CurrentState)
                        {
                            case ConsoleState.Menu:
                                _cts?.Cancel(); 
                                File.WriteAllText(_notePath, "Cancel");
                                Console.Clear();
                                Console.WriteLine("M A I N    M E N U");
                                Console.WriteLine("==================");
                                Console.WriteLine("1 - New note");
                                Console.WriteLine("2 - Search notes");
                                Console.WriteLine("3 - Count notes");
                                Console.WriteLine("4 - Clear notes");
                                Console.WriteLine();
                                Console.WriteLine("Press CONTROL + X to exit.");
                                break;
                            case ConsoleState.NewNote:
                                File.WriteAllText(_notePath, "OK");
                                _cts?.Cancel();
                                _cts = new CancellationTokenSource();
                                Console.Clear();
                                Console.WriteLine("Editing Note...");
                                Console.WriteLine("===============");
                                Console.WriteLine("Press:");
                                Console.WriteLine("    ESCAPE to return to Main Menu");
                                Console.WriteLine("    CONTROL + X to exit.");
                                Console.WriteLine();
                                var fi = new FileInfo(_notePath);
                                _dtInit = fi.LastWriteTime;
                                Task.Run(() =>
                                {
                                    while (true)
                                    {
                                        if (!_cts.Token.IsCancellationRequested)
                                        {
                                            if (new FileInfo(_notePath).LastWriteTime > _dtInit)
                                            {
                                                var text = File.ReadAllText(_notePath);
                                                switch (text)
                                                {
                                                    case "Abort":
                                                        // EXIT regardless of state
                                                        _running.Release();
                                                        break; 
                                                    case "Cancel":
                                                        CurrentState = ConsoleState.Menu;
                                                        break;
                                                    default:
                                                        using (var cnx = new SQLiteConnection(_connectionString))
                                                        {
                                                            var note = JsonConvert.DeserializeObject<Note>(text);
                                                            cnx.InsertOrReplace(note);
                                                        }
                                                        break;
                                                }
                                                CurrentState = ConsoleState.Menu;
                                                return;
                                            }
                                        }
                                        Task.Delay(1000).Wait();
                                    }
                                }, _cts.Token);
                                Process.Start(_editorExe);
                                break;
                            case ConsoleState.SearchNotes:
                                File.WriteAllText(_notePath, "Cancel");
                                Console.Clear();
                                _searchTerm = new SearchTerm();
                                Console.WriteLine("Search Notes");
                                Console.WriteLine("===============");
                                Console.WriteLine("Press:");
                                Console.WriteLine("    ESCAPE to return to Main Menu");
                                Console.WriteLine("    CONTROL + X to exit.");
                                Console.WriteLine();
                                Console.WriteLine("Enter search term:");
                                Console.WriteLine();
                                break;
                            default:
                                // N O O P
                                // In particular DO NOT clead console
                                break;
                        }
                    }
                }
            }
            static DateTime _dtInit = DateTime.MinValue;
            private static CancellationTokenSource _cts = null;
            private static SearchTerm _searchTerm = null;
            private static bool tryGetConsoleKey(out ConsoleKeyInfo key)
            {
                if (Console.KeyAvailable)
                {
                    key = Console.ReadKey(intercept: true);
                    return true;
                }
                else
                {
                    key = new ConsoleKeyInfo();
                    return false;
                }
            }
        }

        // This is just for bringing the Console window to the front in VS debugger.
        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private static string _editorExe =
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "NoteEditor",
                "netcoreapp3.1",
                "NoteEditor.exe");

        private static string _notePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "key_filter_for_console",
                "note.rtf");
    }
    [Table("notes"), DebuggerDisplay("{Rtf}")]
    class Note
    {
        [PrimaryKey]
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Rtf { get; set; }
        public string Text { get; set; }
    }
    class SearchTerm
    {
        public string Text
        {
            get => string.Join(String.Empty, _note);
            set
            {
                Clear();
                Add(value);
            }
        }
        List<string> _note = new List<string>();
        public void Add(string value) => _note.Add(value);

        private void Clear() => _note.Clear();

        public override string ToString() => Text;

        internal void RemoveLast()
        {
            if(_note.Any()) _note.Remove(_note.Last());
        }
    }
}
