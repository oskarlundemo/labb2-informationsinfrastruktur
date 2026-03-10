# Placement Infra Lab 2

Repositoryt innehåller två varianter av Lab 2 (båda på `.NET 9.0`):

- `reference/`: komplett referenslösning (API + UI)
- `starter/`: studentstart med stubbar i upstream-klienterna

## Kör reference

API:

```bash
dotnet run --project /Users/antba159/Documents/New\ project/reference/PlacementService.Api/PlacementService.Api.csproj
```

UI:

```bash
dotnet run --project /Users/antba159/Documents/New\ project/reference/PlacementService.Ui/PlacementService.Ui.csproj
```

Kör API först. UI är konfigurerat att anropa `http://localhost:5000`.

## Kör starter

API:

```bash
dotnet run --project /Users/antba159/Documents/New\ project/starter/PlacementService.Api/PlacementService.Api.csproj
```

UI:

```bash
dotnet run --project /Users/antba159/Documents/New\ project/starter/PlacementService.Ui/PlacementService.Ui.csproj
```

Kör API först. UI är konfigurerat att anropa `http://localhost:5000`.

UI-projekten anropar API-bas-URL från respektive `appsettings.json`.

## Kursdokument (svenska)

Studentversioner:
- `docs/student/Lab2-Student-Instruktion.md`
- `docs/student/Prelab-Student.md`
- `docs/student/Blazor-Cookbook-Student.md`

Lärarversioner:
- `docs/teacher/Lab2-Teacher-Guide.md`
- `docs/teacher/Prelab-Teacher-Notes.md`
- `docs/teacher/Blazor-Cookbook-Teacher.md`
- `docs/teacher/Lab2-Steg-for-steg-med-losningar-och-svar.md`

Tidigare samlade versioner finns kvar i `docs/` för referens.
