Your question is about detecting CONTROL X but I see your additional comment that goes to the heart of the matter:
>[...]Where shall I write the code which I want to implement in the console application. For example like taking user input from console, then do something with that input value and write to console. "

My answer, basically, is to **treat them the same**, polling for both CONTROL X _and_ user input values in a single task loop but doing this _strategically_:

- _Avoid_ using up all the core cycles mindlessly spinning on a bool.
- _Avoid_ blocking the process with a `ReadKey` when no character is waiting.
- _Ensure_ polling is frequent enough to be responsive, but not enough to load the core unnecessarily.

This last point is critical because the responsiveness is not _constant_ but rather relies on the current activity. This SQLite database program (browse or [clone](https://github.com/IVSoftware/key_filter_for_console.git)) will demonstrate. The user has only 5 choices (_one_ of them is CONTROL X) and sampling a few times per second is plenty. If option `2` is chosen the user will enter a search term which requires a _much_ higher sampling rate (maybe throttling to ~1 ms). The point is, we save that for when we need it.

![Main Menu]

***
**What CONTROL X does**
When detected, the task loop will return. But if a task loop is "all there is" then what keeps the app from dropping out? One solution is to have the `ConsoleApplication.Run()` method return a SemaphoreSlim, and simply call `Wait` on it, Now the `Main` will not exit until CONTROL X releases the semaphore. 

    static void Main(string[] args)
    {
        ConsoleApplication.Run().Wait();
    }

Under the hood, the `Run` method will start the process of filtering for user input by calling `Task.Run(() => filterKeyboardMessages())`.
***
**Where the code goes**

The activities are determined by the state. For this app, it is one of:

    enum ConsoleState
    {
        Menu,
        NewNote,
        SearchNotes,
        RecordsShown,
    }

The state is determined by user input. A simplified structure might look like this.

   while (true)
    {
        while (tryGetConsoleKey(out ConsoleKeyInfo keyInfo))
        {
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
                    . . .
                    break;
                case ConsoleState.SearchNotes:
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Escape:
                            CurrentState = ConsoleState.Menu;
                            break;
                        case ConsoleKey.Enter:
                            var query = _searchTerm.ToString();
                            continue;
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



When the user is in the _state_ of choosing the _activity_, sampling a few times a second is plenty (and that includes CONTROL X).  



The thing is, once you do this for CONTROL X, what stop with that?


How this is done depends on what kind of work the app is doing. In order to put this in some kind of meaningful context, I have made a 

