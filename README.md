# EF6 Perf Demo

1. Update the connection string in Program.cs to point to your SQL Server database. Make sure you
   use a separate database because it will drop it if the schema does not match whats expected
2. Run it
3. After the first run you can comment out the call to `Seed()` to skip repopulating the db

If you run with the debugger, you can see all the EF generated SQL in the Debug output window