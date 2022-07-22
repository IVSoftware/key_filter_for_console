Your question is about detecting **CONTROL X** and your additional comment goes to the heart of the matter by asking:
>[...] Where shall I write the code which I want to implement in the console application. For example like taking user input from console, then do something with that input value and write to console. "

My answer, basically, is to **treat them the same**, polling for both **CONTROL X** _and_ user input values in a single task loop but doing this _strategically_:

- **Avoid** using up all the core cycles mindlessly spinning on a bool.
- **Avoid** blocking the process with a `ReadKey` when no character is waiting.
- **Ensure** polling is frequent enough to be responsive, but not enough to load the core unnecessarily.

This last point is crucial because the responsiveness is _not constant_ but rather relies on the current activity. I have put together the following state-driven SQLite database program to demonstrate. On the main screen, the user has only 5 choices (_one_ of them is **CONTROL X**) and sampling a few times per second is plenty. If option `2` is chosen the user will enter a search term which requires a _much_ higher sampling rate (maybe throttling to ~1 ms). The point is, we save that intensive polling for when we really need it.

![Main Menu](https://github.com/IVSoftware/key_filter_for_console/blob/master/key_filter_for_console/ReadMe/small%20screenshot.png)

***
**What **CONTROL X** does**

Obviously the loop will return once this is detected, but if a `Task` is "all there is" then what keeps the app from dropping out in the meantime? One solution is to have `ConsoleApplication.Run()` return a synchronization object like `SemaphoreSlim` and simply call `Wait` on it. Now the `Main` will not exit until **CONTROL X** releases the semaphore. 

    static void Main(string[] args)
    {
        ConsoleApplication.Run().Wait();
    }

Under the hood, the `Run` method will start the process of filtering for user input by calling

    Task.Run(() => filterKeyboardMessages());
***
**Where the code goes**

The activities are determined by the state. For this particular app, it is one of:

    enum ConsoleState{ Menu, NewNote, SearchNotes, RecordsShown, }

This in turn is determined by user input. Every keystroke passes through this filter, where simplified outline in `filterKeyboardMessages` might be something like this.

    while (true)
    {
        while (tryGetConsoleKey(out ConsoleKeyInfo keyInfo))
        {
            // C O N T R O L    X 
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
                    // Invoke editor to make a new note
                    break;
                case ConsoleState.SearchNotes:
                    // - Take rapid user input keystrokes.
                    // - Be WAY more responsive when typing is allowed.
                    await Task.Delay(1);
                    // When ENTER is detected, perform the query                    
                    CurrentState = ConsoleState.RecordsShown;
                    continue;
                case ConsoleState.RecordsShown:
                    // - The search results are now output to the screen.
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Escape:
                            CurrentState = ConsoleState.Menu;
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

***
**Sample keys without blocking**

The `tryGetConsoleKey` is a critical piece that only blocks to read a character if it's already known to be available.

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

***
**TEST**

The following screenshots demonstrate the database application doing real work which at any given moment can be exited with **CONTROL X**.

_Main view and note editor:_

![main and edit note](https://github.com/IVSoftware/key_filter_for_console/blob/master/key_filter_for_console/ReadMe/menu-driven-canonical.1-2.png)


_Make another two notes..._

![make two more notes](https://github.com/IVSoftware/key_filter_for_console/blob/master/key_filter_for_console/ReadMe/menu-driven-canonical.3-4.png)

_Now enter the search term. Something like 'tom' or 'canonical':_
![enter a search term](https://github.com/IVSoftware/key_filter_for_console/blob/master/key_filter_for_console/ReadMe/menu-driven-canonical.5-6.png)


_The record results are displayed in the `RecordsShown` activity:_
![query result](https://github.com/IVSoftware/key_filter_for_console/blob/master/key_filter_for_console/ReadMe/menu-driven-canonical.7-8.png)


