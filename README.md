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
  - `BlightPlaythroughTests` joue Blight de bout en bout par les commandes du MJ, avec les deux fins et le rejeu de l'historique.
  - `GameEngineTests` vérifie les règles sur un mini-pack : IA, Aggro, groupes, Urgence, piste d'initiative.
  - `ScenarioCheckerTests.Check_BlightOfRhamsted_SameResultAsCheckScenarioPy` garantit la parité avec `tools/check_scenario.py`.
  - `GetScenarioAsync_Blight_KeepsEveryFieldOfTheFile` vérifie que le modèle ne perd aucun champ du fichier.
- **Tests d'intégration** : ils tournent sur un mini-pack maison (`tests/Taleweaver.IntegrationTests/Fixtures/`), plus un test sur le vrai contenu.

## API

Routes, commandes et fonctionnement du moteur : [docs/design/backend-architecture.md](../docs/design/backend-architecture.md) §4 et §5.

Les parties sont enregistrées dans `%APPDATA%/Middara/saves`, un fichier par partie. Le dossier se change avec `Content:SavesPath`.

Jouer une partie, côté MJ :
1. `POST /api/games` avec `{ "scenario": "um1:scenario/blight-of-rhamsted", "party": ["Nightingale", "Rook"] }` ;
2. répondre à chaque question par `POST /api/games/{id}/answer` (`{ "option": "continue" }`, `"yes"`, `"0"`, ou `{ "value": 12 }`) ;
3. en rencontre :
   - avancer avec `POST …/encounter/next-turn` ;
   - signaler ce qui se passe sur le plateau : dégâts, effets, défaite, jeton activé, condition de fin ;
4. `POST /api/games/{id}/undo` en cas d'erreur.

## Faire évoluer le format

- Un nouveau champ du format se déclare dans les records de `src/Taleweaver.Api/Content/`.
- Une action de scénario (`do`) inconnue n'a pas besoin de nouveau type : ses champs non typés sont conservés dans `ScenarioAction.Extra`.
- Le test d'aller-retour sur Blight signale tout champ oublié.
