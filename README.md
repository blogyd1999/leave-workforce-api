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

## Local PostgreSQL

Requires Docker with Compose v2 (Colima works). The image is pinned once in `deploy/postgres.env` (`postgres:18`). The API does not connect to the database until entry 1.3.

```sh
cp deploy/db/dev.env.example deploy/db/dev.env   # then replace every CHANGE_ME; the file is gitignored
```

`deploy/db/dev.env` must set `POSTGRES_DB`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `MIGRATOR_PASSWORD`, `APP_RW_PASSWORD` and `RETENTION_JOB_PASSWORD` (simple values; placeholders are refused). The port is bound to 127.0.0.1 only.

```sh
deploy/db/db.sh up       # start PostgreSQL 18 and apply deploy/db/roles.sql
deploy/db/db.sh down     # stop; data stays in the volume
deploy/db/db.sh reset    # stop and delete the volume; the next `up` is a fresh database
deploy/db/db.sh roles    # rerun roles.sql (idempotent; re-applies passwords)
deploy/db/db.sh verify   # check version, roles, wrong passwords and privileges
deploy/db/db.sh psql     # psql as the superuser inside the container
```

Connection string example, with the password taken from the environment:

```sh
set -a; . deploy/db/dev.env; set +a   # loads the values below into your shell
export ConnectionStrings__App="Host=127.0.0.1;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=app_rw;Password=$APP_RW_PASSWORD"
```
