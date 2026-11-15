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

## Front (`web/`)

Vue 3 + TypeScript strict, Vite, PrimeVue 4, TanStack Query, Pinia, vue-i18n (FR/EN). Standards : skill `vue-frontend`.

⚠️ **PrimeVue reste en 4.5.x** (MIT). La version 5 passe sous licence PrimeUI, qui exige une clé, même pour la licence gratuite.

```bash
cd taleweaver/web && npm install && npm run dev
```

- Lancer aussi l'API (section précédente). Le serveur Vite (http://localhost:5173) redirige `/api` vers http://localhost:5145.
- Dans l'app de bureau Claude, `.claude/launch.json` déclare `taleweaver-api` et `taleweaver-web`.
- Pages :
  - accueil : scénarios et parties sauvegardées ;
  - partie, en rencontre (vue MJ, pensée PC d'abord) : barre de la rencontre (round, Urgence, piste d'initiative, « Tour suivant »), la table au centre, et à droite le **tour en cours** (toujours visible : à qui c'est, instructions d'IA gardées jusqu'au tour suivant, question en attente) au-dessus d'onglets Combattants (liste compacte ; une ligne sélectionne la figurine et ouvre ses actions sur la table) / Journal / Rencontre (conditions de fin, jetons, règles spéciales). Sous 1000 px de large, tout passe en une colonne ;
  - partie, hors rencontre : le récit en colonne de lecture, avec sa question.
- **Table** (`src/features/table/`), la carte, utilisable seule sur `/games/{id}/table` (bouton « Table seule » ; elle se recharge toutes les 2 s pour suivre la partie jouée depuis la vue MJ) :
  - `BoardCanvas` dessine (images locales `/api/assets/…`) et traduit les gestes : sélectionner, glisser une figurine, déplacer la vue, zoomer (molette, boutons) ;
  - `TableSelection` propose les actions de ce qui est sélectionné : dégâts, effets, Vaincre ; Activer un jeton ; Révéler une zone cachée ;
  - `TableView` assemble les deux et ne parle qu'en commandes de rencontre ; `lib/layout.ts` calcule ce qu'on voit (fonction pure, testée), `lib/viewport.ts` le zoom ;
  - déplacements facultatifs : glisser la figurine, ou la sélectionner puis toucher une case (pratique sur tablette) ; ce qui n'a pas de case est listé « À placer » et se glisse de la liste vers la carte ;
  - zoom : molette, boutons, ou deux doigts sur tablette ; bouton plein écran ;
  - brouillard : les tuiles d'une zone cachée sont grisées, et ce qui s'y trouve est masqué jusqu'à « Révéler la zone ».
- **Affrontement** (`src/features/duel/`), une attaque ou un sort avec les dés physiques de la table, dans **sa propre fenêtre** (modale, plein écran sur tablette ; Échap la ferme et rend la table telle quelle). Elle s'ouvre par « Attaquer » sur une figurine sélectionnée, ou « Résoudre l'attaque » quand l'IA dit d'attaquer, depuis la vue MJ comme depuis la table seule :
  - à gauche l'attaquant (figurine, carte consultable en grand, dés de combat, Empower, tenaille, allié ou terrain gênant, bonus, perforant, capacités de symboles), à droite la cible (PV, Défense, Armure, esquive), bouton « Inverser » ;
  - au centre les **chances** (toucher, blesser, dégâts moyens, hors de combat), masquables par un interrupteur (préférence gardée) ;
  - puis **les dés à toucher** : chaque dé montre ses six faces, le MJ touche celle obtenue (comme les ponts « dés physiques » des tables virtuelles) ; total et symboles se calculent seuls. « Saisie libre » garde la saisie d'un total et de symboles (relances, symboles venus d'ailleurs) ;
  - le résultat déroule les étapes du livre (écart, symboles dépensés pour le plus de dégâts, Armure, dégâts finaux) et inflige les dégâts au combattant en un clic ; « Nouveau jet » enchaîne (Chains of Perdition…) ;
  - sort ou capacité type sort : faces du dé de lancer (et du dé noir) puis des dés de Conviction de la cible ;
  - images : figurines (`/api/catalog/board`), cartes des combattants, icônes de dés, de symboles et de stats de `assets/templates/discipline-card/icons/` ;
  - aventuriers : Défense 7, Conviction 2 violets et lancer violet par défaut (toutes les cartes de l'Acte 1) ; armes et armure viennent des objets, que la partie ne suit pas : l'appli garde les dernières valeurs saisies, par partie, dans le navigateur ;
  - le calcul (`lib/attack.ts`, `lib/spell.ts`, `lib/dice.ts`) est testé sur les exemples chiffrés du livre de règles (p. 45, 54, 55, 65).
- ⚠️ Les images appartiennent à Succubus Publishing : l'API les lit sur le disque local et ne doit jamais être exposée en ligne avec elles.
- Les textes de l'interface et la mise en forme du journal sont dans `src/shared/i18n/messages.ts`. Le contenu du jeu garde ses propres textes `{ en, fr }`.
- Vérifications :

```bash
npm run typecheck && npm run lint && npm test && npm run e2e
```

`npm run e2e` lance lui-même l'API (sauvegardes dans un dossier temporaire) et Vite, puis joue Blight dans Edge (`channel: 'msedge'` : aucun navigateur à télécharger).

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
