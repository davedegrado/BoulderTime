EF Core migrations for the `bouldertime` schema live in this folder.

Generate after changing the model (from `backend/`):

    dotnet ef migrations add <Name> \
      --project src/BoulderTime.Infrastructure \
      --startup-project src/BoulderTime.Api \
      --output-dir Persistence/Migrations

Never hand-edit the model snapshot.
