// EfCoreTransactions -- your workspace.
//
// This is where you write the code from EXERCISE.md. Work through the parts in
// order, replacing/growing this file (and adding new ones) as you go:
//
//   Part 1  Implicit transactions ........ one SaveChanges(), two changes
//   Part 2  Explicit transactions ........ BeginTransactionAsync/Commit/Rollback
//   Part 3  Savepoints ................... CreateSavepointAsync/RollbackToSavepointAsync
//   Part 4  Optimistic concurrency ....... the xmin concurrency token
//   Part 5  Isolation levels ............. Read Committed vs. Serializable
//   Part 6  Unit of work ................. ITransferService / UnitOfWork
//
// appsettings.json already points at the "efcore_transactions" database that
// the module's docker-compose.yml provisions, and is set to copy to the
// output directory, so `Database.GetConnectionString("BankDb")` will find it.
//
// Run your work:        dotnet run
// Check it:              dotnet test ../tests
// Compare afterwards:   ../solution/
//
// Try each part yourself before opening the reference solution. Reading it
// first is the fastest way to feel productive and learn nothing.

Console.WriteLine("EfCoreTransactions workspace -- start with Part 1 of EXERCISE.md");
