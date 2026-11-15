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
  - partie, en rencontre (vue MJ, pensée PC d'abord) : barre de la rencontre (round, Urgence, piste d'initiative, « Tour suivant »), la table au centre, et à droite le **tour en cours** (toujours visible : à qui c'est, instructions d'IA gardées jusqu'au tour suivant, question en attente) au-dessus d'onglets Combattants (liste compacte des seuls combattants déjà sur la table, pour ne rien dévoiler : ni « à placer », ni sous le brouillard, sauf celui qui joue ; une ligne sélectionne la figurine et ouvre ses actions sur la table) / Journal / Rencontre (conditions de fin, jetons, règles spéciales). Sous 1000 px de large, tout passe en une colonne ;
  - partie, hors rencontre : le récit en colonne de lecture, avec sa question.
- **Table** (`src/features/table/`), la carte, utilisable seule sur `/games/{id}/table` (bouton « Table seule » ; elle se recharge toutes les 2 s pour suivre la partie jouée depuis la vue MJ) :
  - `BoardCanvas` dessine (images locales `/api/assets/…`) et traduit les gestes : sélectionner, glisser une figurine, déplacer la vue, zoomer (molette, boutons) ;
  - `TableSelection` propose les actions de ce qui est sélectionné : dégâts, effets, Vaincre ; Activer un jeton ; Révéler une zone cachée ;
  - `TableView` assemble les deux et ne parle qu'en commandes de rencontre ; `lib/layout.ts` calcule ce qu'on voit (fonction pure, testée), `lib/viewport.ts` le zoom ;
  - déplacements facultatifs : glisser la figurine, ou la sélectionner puis toucher une case (pratique sur tablette) ; ce qui n'a pas de case est listé « À placer » et se glisse de la liste vers la carte. Pour ne rien dévoiler, les combattants à placer n'y sont pas nommés (sauf celui qui joue) : dans la vue MJ, le bouton « Combattants à placer » les affiche ; sur la table seule, ils n'apparaissent jamais ;
  - zoom : molette, boutons, ou deux doigts sur tablette ; bouton plein écran ;
  - brouillard : les tuiles d'une zone cachée sont grisées, et ce qui s'y trouve est masqué jusqu'à « Révéler la zone ».
- **Affrontement** (`src/features/duel/`), une attaque ou un sort avec les dés physiques de la table, dans **sa propre fenêtre** (modale, plein écran sur tablette ; Échap la ferme et rend la table telle quelle). Elle s'ouvre par « Attaquer » sur une figurine sélectionnée, ou « Résoudre l'attaque » quand l'IA dit d'attaquer, depuis la vue MJ comme depuis la table seule :
  - à gauche l'attaquant (figurine, carte consultable en grand, dés de combat, Empower, tenaille, allié ou terrain gênant, bonus, perforant, capacités de symboles), à droite la cible (PV, Défense, Armure, esquive), bouton « Inverser » ;
  - sous la cible, **le plateau autour de l'affrontement** (la fenêtre cache la table) : la cible entourée en rouge, l'attaquant en orange, les autres figurines estompées. Toucher une figurine en fait la cible ; on peut y zoomer et s'y déplacer, mais rien n'y bouge ;
  - attaquant et cible se choisissent parmi les figurines **posées sur une tuile visible** : pas celles sous le brouillard, « À placer » ou lâchées hors des tuiles (l'aide à l'IA suit la même règle). Sans carte chargée, toutes restent proposées ;
  - au centre les **chances** (toucher, blesser, dégâts moyens, hors de combat), masquables par un interrupteur (préférence gardée) ;
  - puis **les dés à toucher** : chaque dé montre ses six faces, le MJ touche celle obtenue (comme les ponts « dés physiques » des tables virtuelles) ; total et symboles se calculent seuls. « Saisie libre » garde la saisie d'un total et de symboles (relances, symboles venus d'ailleurs) ;
  - le résultat déroule les étapes du livre (écart, symboles dépensés pour le plus de dégâts, Armure, dégâts finaux) et inflige les dégâts au combattant en un clic ; « Nouveau jet » enchaîne (Chains of Perdition…) ;
  - sort ou capacité type sort : faces du dé de lancer (et du dé noir) puis des dés de Conviction de la cible ;
  - images : figurines (`/api/catalog/board`), cartes des combattants, icônes de dés, de symboles et de stats de `assets/templates/discipline-card/icons/` ;
  - aventuriers : Défense 7, Conviction 2 violets et lancer violet par défaut (toutes les cartes de l'Acte 1) ; armes et armure viennent des objets, que la partie ne suit pas : l'appli garde les dernières valeurs saisies, par partie, dans le navigateur ;
  - le calcul (`lib/attack.ts`, `lib/spell.ts`, `lib/dice.ts`) est testé sur les exemples chiffrés du livre de règles (p. 45, 54, 55, 65).
- **Aide à l'IA des combattants** (`src/features/ai/`), lue sur les positions de la table : l'appli **suggère**, le MJ répond et déplace.
  - Question d'IA : la condition imprimée est lue (« opponent adjacent », « within Range/Reach N », « within SOI » = 4, « Can it Move and Attack… », « Does … have Damage? ») et l'appli propose Oui ou Non avec la raison (« le plus proche : Rook à 3 cases »). Le bouton suggéré est mis en avant ; une condition non comprise (Burrowed, jetons, Esper, alliés…) reste une simple question.
  - Instructions données : pour chaque Attaque ou Sort, les cibles à portée et la cible suggérée (« nearest », « farthest », « same target », sinon l'ordre de la piste d'initiative, p. 62) avec « Résoudre contre X », qui ouvre l'affrontement sur cette cible (côté Sort pour un sort). Un critère que la table ne montre pas (« with the most Damage », Conviction…) est affiché, et les candidats sont listés pour que le MJ tranche.
  - Pour chaque MOVE, la case d'arrivée suggérée : déplacement orthogonal de la valeur de Movement, jamais à travers un adversaire, à travers un allié sans s'y arrêter (2 points pour en sortir), « up to » = aller au maximum, « within » = ne bouger que si nécessaire (p. 41, 62). La case est surlignée sur la table, avec le trajet, dans la vue MJ comme sur la table seule, et « Déplacer X » l'applique en un clic. Les cibles suggérées sont entourées en pointillés.
  - Le plan part de la case où était la figurine quand l'IA a parlé, puis enchaîne les étapes (une attaque après un MOVE se mesure depuis la case d'arrivée).
  - **Limite** : les tuiles n'ont pas encore leur terrain (murs, terrain bloquant, ligne de vue). Les distances sont donc à vol d'oiseau, et l'appli le rappelle sous chaque suggestion. Sur les cartes de l'Acte 1, environ 3 conditions sur 4 et 2 instructions sur 3 sont comprises ; le reste reste au MJ.
- **Atelier** (`src/features/workshop/`, lien « Atelier » en haut, `/workshop`), pour créer ses primes et rencontres :
  - la liste : « Mes scénarios » (ouvrir, supprimer), les scénarios officiels en lecture seule (« Dupliquer » en fait une copie modifiable), « Nouveau scénario » ;
  - l'éditeur (`/workshop/{nom}`) : le **graphe** des étapes au centre, dessiné avec Vue Flow (MIT). Chaque étape a ses sorties à droite : « Suite », les options d'un choix, ou les conditions de victoire et de défaite. On tire un lien d'une sortie vers une étape ou une fin (victoire, défaite) pour créer un embranchement. On glisse le bout d'un lien pour le changer, ou on le sélectionne puis Suppr ;
  - à côté, la **fiche** de l'étape choisie : scène (titre, texte lu, suite ou choix des joueurs avec ses options) ou rencontre (nom, introduction, niveau de loot, type de lieu, conditions de fin avec récompense et suite, règles spéciales). Les textes s'écrivent dans la langue de l'interface, et les autres langues sont gardées ;
  - la carte, la mise en place, les jetons et les événements ne s'éditent pas encore (prochaine étape : l'éditeur de carte) ; ceux d'un scénario dupliqué sont conservés tels quels ;
  - vérification en direct par l'API (pastille « Aucun problème » ou nombre de problèmes, signalés sur les étapes), « Enregistrer » (ou Ctrl+S), puis « Tester », qui enregistre et lance une partie de ce scénario ;
  - la logique du graphe (sorties, liens, choix, mise en page par colonnes) est dans `lib/graph.ts`, testée ; la position de chaque étape est gardée dans le champ `editor` du scénario.
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

Les scénarios de l'atelier (`homebrew:scenario/<nom>`) sont dans `scenarios/homebrew/` (`Content:HomebrewDirectory`). Ce sont les seuls que l'API écrit (`PUT`) ou supprime (`DELETE /api/scenarios/homebrew/{nom}`) ; les scénarios officiels restent en lecture seule.

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
