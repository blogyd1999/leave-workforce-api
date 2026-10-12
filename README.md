# leave-workforce-api
Implimenting using BMAD

## Build and run

Requires the .NET SDK pinned in `global.json` (10.0.103).

```sh
dotnet build LeaveWorkforce.slnx
dotnet run --project src/Api --urls http://127.0.0.1:5199
curl -i http://127.0.0.1:5199/health   # 200 {"status":"up"}
dotnet test --solution LeaveWorkforce.slnx   # exits 8 ("zero tests") until Architecture.Tests and Concurrency.Tests gain tests in entries 1.6 and 1.7
```

The connection string is read from `ConnectionStrings:App` (for example the `ConnectionStrings__App` environment variable); do not commit secrets.
