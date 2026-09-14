namespace AsyncAwait.Examples;

/// <summary>
/// Part 6: the anti-patterns, kept compilable so you can step through them.
///
/// Note the #pragma directives below. This repository builds CS4014 (unawaited
/// task) and CS1998 (async without await) as ERRORS, so the only way to write
/// these bugs is to silence the compiler first. That is the lesson: if you ever
/// find yourself adding one of these pragmas to real code, stop and look again.
/// </summary>
public class AsyncPitfalls
{
    // WRONG: async void. The exception below cannot be caught by a caller --
    // it is raised on the synchronization context and usually kills the
    // process. Only ever acceptable for a genuine event handler.
    public async void DontDoThisAsync()
    {
        await Task.Delay(100);
        throw new InvalidOperationException("Can't catch this!");
    }

    // CORRECT: async Task, so the caller can await and catch.
    public async Task DoThisAsync()
    {
        await Task.Delay(100);
    }

    // WRONG: blocking on async code. In any context with a
    // SynchronizationContext this deadlocks: the continuation needs the thread
    // that .Result is currently blocking.
    public void SyncOverAsyncWrong()
    {
        var result = DoThisAsync();
        result.Wait();
    }

    // CORRECT: async all the way up the call stack.
    public async Task AsyncAllTheWay()
    {
        await DoThisAsync();
    }

    // WRONG: starting tasks and never awaiting them. The method returns while
    // the work is still in flight, and any exception is lost.
    public Task FireAndForgetWrong()
    {
        for (int i = 0; i < 5; i++)
        {
#pragma warning disable CS4014
            DoThisAsync(); // Fire and forget - BAD!
#pragma warning restore CS4014
        }

        return Task.CompletedTask;
    }

    // CORRECT: collect the tasks, then await them together.
    public async Task CollectAndAwaitCorrect()
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(DoThisAsync());
        }

        await Task.WhenAll(tasks);
    }

    // WRONG: Task.Run around async I/O. It burns a pool thread to wait for
    // something that was never going to block a thread in the first place.
    public async Task<string> TaskRunForIOWrong(string path)
    {
        return await Task.Run(async () => await File.ReadAllTextAsync(path));
    }

    // CORRECT: call the async I/O directly.
    public async Task<string> AsyncIOCorrect(string path)
    {
        return await File.ReadAllTextAsync(path);
    }
}
