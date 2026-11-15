# Taleweaver

Back-end de l'outil : éditeur de scénarios et compagnon de jeu pour Middara. Architecture : [docs/design/backend-architecture.md](../docs/design/backend-architecture.md). Standards : skill `dotnet-backend`.

⚠️ L'API sert le contenu sous droits du dépôt (`packs/`, `scenarios/`, `assets/`) : elle écoute sur `localhost` uniquement, pour un usage personnel.

## Lancer

```bash
cd taleweaver && dotnet run --project src/Taleweaver.Api
```

- API sur http://localhost:5145.
- Document OpenAPI sur `/openapi/v1.json` (Development).
- Le contenu est lu depuis la racine du dépôt : section `Content` de `appsettings.json` (`RootPath`, `EnabledPacks`).

## Tester

```bash
cd taleweaver && dotnet test
```

- Lancer `dotnet test` depuis `taleweaver/`. C'est là que `global.json` active Microsoft Testing Platform : avec le SDK .NET 10, le mode VSTest refuse xUnit v3.
- **Tests unitaires** : ils lisent le vrai contenu du dépôt (packs et Blight of Rhamsted).
  - `ScenarioCheckerTests.Check_BlightOfRhamsted_SameResultAsCheckScenarioPy` garantit la parité avec `tools/check_scenario.py`.
  - `GetScenarioAsync_Blight_KeepsEveryFieldOfTheFile` vérifie que le modèle ne perd aucun champ du fichier.
- **Tests d'intégration** : ils tournent sur un mini-pack maison (`tests/Taleweaver.IntegrationTests/Fixtures/`), plus un test sur le vrai contenu.

## API (premier incrément)

| Méthode | Route | Usage |
|---|---|---|
| GET | `/api/packs` | packs disponibles, activés ou non |
| GET | `/api/packs/{pack}/combatants/{name}` | carte de combattant (`um1` / `cave-sickle-1-3-common`) |
| GET | `/api/catalog?kind=&q=&pack=&limit=` | recherche dans le catalogue des packs activés |
| GET | `/api/scenarios` · `/api/scenarios/{pack}/{name}` | liste, contenu (`um1` / `blight-of-rhamsted`) |
| POST | `/api/scenarios/validate` | rapport du vérificateur : `errors`, `warnings`, `cards` |

## Faire évoluer le format

- Un nouveau champ du format se déclare dans les records de `src/Taleweaver.Api/Content/`.
- Une action de scénario (`do`) inconnue n'a pas besoin de nouveau type : ses champs non typés sont conservés dans `ScenarioAction.Extra`.
- Le test d'aller-retour sur Blight signale tout champ oublié.
