# Taleweaver

Back-end de l'outil : éditeur de scénarios et compagnon de jeu pour Middara. Architecture : [docs/design/backend-architecture.md](../docs/design/backend-architecture.md). Standards : skill `dotnet-backend`.

⚠️ L'API sert le contenu sous droits du dépôt (`packs/`, `scenarios/`, `assets/`) : elle écoute sur `localhost` uniquement, pour un usage personnel.

## Lancer

```bash
cd taleweaver && dotnet run --project src/Taleweaver.Api
```

- API sur http://localhost:5145.
- Document OpenAPI sur `/openapi/v1.json` (Development).
- Le contenu est lu depuis la racine du dépôt : section `Content` de `appsettings.json` (`RootPath`, `EnabledPacks` : vide = tous les packs de `packs/`, écartés si une dépendance manque ; un pack peut remplacer des cartes d'un autre via `overrides`).

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
  - partie, en rencontre (vue MJ, pensée PC d'abord) : barre de la rencontre (round, Urgence, piste d'initiative, « Tour suivant »), la table au centre, et à droite le **tour en cours** (toujours visible : à qui c'est, instructions d'IA gardées jusqu'au tour suivant, question en attente) au-dessus d'onglets Combattants (liste compacte des seuls combattants déjà sur la table, pour ne rien dévoiler : ni « à placer », ni sous le brouillard, sauf celui qui joue ; une ligne, ou la figurine touchée sur la table, ouvre ses actions **sous la liste**) / Journal / Rencontre (conditions de fin, jetons, règles spéciales). Sous 1000 px de large, tout passe en une colonne ;
  - partie, hors rencontre : le récit en colonne de lecture, avec sa question.
- **Table** (`src/features/table/`), la carte, utilisable seule sur `/games/{id}/table` (bouton « Table seule » ; elle se recharge toutes les 2 s pour suivre la partie jouée depuis la vue MJ) :
  - `BoardCanvas` dessine (images locales `/api/assets/…`) et traduit les gestes : sélectionner, glisser une figurine, déplacer la vue, zoomer (molette, boutons) ;
  - `TableSelection` propose les actions de ce qui est sélectionné : barre de dégâts, Infliger/Soigner, **effets** (les 11 Effets des règles dans une liste, avec leur texte, ou un effet maison tapé ; aussi sur les aventuriers ; `GET /api/catalog/effects`), Attaquer, Vaincre ; Activer un jeton ; Révéler une zone cachée. Dans la vue MJ, sous la liste des combattants (`selection-outside`) ; sur la table seule, par-dessus le plateau ;
  - `TableView` assemble les deux et ne parle qu'en commandes de rencontre ; `lib/layout.ts` calcule ce qu'on voit (fonction pure, testée), `lib/viewport.ts` le zoom ;
  - déplacements facultatifs : glisser la figurine, ou la sélectionner puis toucher une case (pratique sur tablette) ; ce qui n'a pas de case est listé « À placer » et se glisse de la liste vers la carte. Pour ne rien dévoiler, les combattants à placer n'y sont pas nommés (sauf celui qui joue) : dans la vue MJ, le bouton « Combattants à placer » les affiche ; sur la table seule, ils n'apparaissent jamais ;
  - zoom : molette, boutons, ou deux doigts sur tablette ; bouton plein écran ;
  - brouillard : les tuiles d'une zone cachée sont grisées, et ce qui s'y trouve est masqué jusqu'à « Révéler la zone ».
- **Affrontement** (`src/features/duel/`), une attaque ou un sort avec les dés physiques de la table, dans **sa propre fenêtre** (modale, plein écran sur tablette ; Échap la ferme et rend la table telle quelle). Les **effets** notés sur les deux figurines sont comptés : Darkness (−1 au jet de l'attaquant ; la cible ne peut plus esquiver), Barrier (+1 Armure), Courage (+1 bouclier et +1 livre), Nemesis (+2 dégâts physiques), Wilt (+2 dégâts irréductibles) ; Paralyze, Tether, Wilt et Nemesis sur un sort sont rappelés au MJ. Poison, Condemn et Haste sont rappelés au début du tour de la figurine (journal). Elle s'ouvre par « Attaquer » sur une figurine sélectionnée, ou « Résoudre l'attaque » quand l'IA dit d'attaquer, depuis la vue MJ comme depuis la table seule :
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
  - Question d'IA : la condition imprimée est lue (« opponent adjacent », « within Range/Reach N », « within SOI » = 4, « in Line of Sight », « Can it Move and Attack… », « Does … have Damage? », « Is there a figure within SOI with an Effect? », « … ally within SOI without Barrier? ») et l'appli propose Oui ou Non avec la raison (« le plus proche : Rook à 3 cases »). Le bouton suggéré est mis en avant ; une condition non comprise (Burrowed, jetons, Esper, alliés…) reste une simple question.
  - Instructions données : pour chaque Attaque ou Sort, les cibles à portée et la cible suggérée (« nearest », « farthest », « same target », sinon l'ordre de la piste d'initiative, p. 62) avec « Résoudre contre X », qui ouvre l'affrontement sur cette cible (côté Sort pour un sort). Un effet nommé (« prioritizing an opponent with Darkness », « without Paralyze », « with the most Effects ») classe les cibles d'après les effets notés. Un critère que la table ne montre pas (« with the most Damage », Conviction…) est affiché, et les candidats sont listés pour que le MJ tranche.
  - Pour chaque MOVE, la case d'arrivée suggérée : déplacement orthogonal de la valeur de Movement, jamais à travers un adversaire, à travers un allié sans s'y arrêter (2 points pour en sortir), « up to » = aller au maximum, « within » = ne bouger que si nécessaire (p. 41, 62). La case est surlignée sur la table, avec le trajet, dans la vue MJ comme sur la table seule, et « Déplacer X » l'applique en un clic. Les cibles suggérées sont entourées en pointillés.
  - Le plan part de la case où était la figurine quand l'IA a parlé, puis enchaîne les étapes (une attaque après un MOVE se mesure depuis la case d'arrivée).
  - **Terrain et ligne de vue** (livre de règles p. 21 et 42-43), d'après les **jetons de terrain posés** : pas de déplacement dans un terrain obstruant ni dangereux, 2 points pour quitter un terrain gênant ou l'eau ; ligne de vue tirée de centre à centre, coupée par le terrain obstruant, les bords des tuiles et les adversaires (au choix des joueurs dans un coin), avec le malus de −1 (terrain gênant, allié sur la ligne, −2 au plus) affiché à côté de la cible. La SOI ne s'arrête qu'au terrain obstruant.
  - Le terrain **imprimé** sur les tuiles (relevé à l'œil dans `packs/um1/tiles.json`, tourné avec la tuile ; ignoré si la rotation est inconnue) est fusionné avec les jetons de terrain. L'**élévation** imprimée (puis les zones `elevation` du scénario, prioritaires) compte aussi (règles p. 22) : ligne de vue entre deux niveaux par-dessus des cases plus basses seulement (la figurine du haut doit être au bord), figurines plus basses ignorées, +1 au jet d'attaque depuis plus haut (affiché), adjacence et Allonge réduites au-delà d'un écart de 1, montée de 1 et descente de 2 au plus. L'appli rappelle sous chaque suggestion de vérifier sur la table. Sur les cartes de l'Acte 1, environ 3 conditions sur 4 et 2 instructions sur 3 sont comprises ; le reste reste au MJ.
- **Atelier** (`src/features/workshop/`, lien « Atelier » en haut, `/workshop`), pour créer ses primes et rencontres :
  - la liste : « Mes scénarios » (ouvrir, supprimer), les scénarios officiels en lecture seule (« Dupliquer » en fait une copie modifiable), « Nouveau scénario » ;
  - l'éditeur (`/workshop/{nom}`) : le **graphe** des étapes au centre, dessiné avec Vue Flow (MIT). Chaque étape a ses sorties à droite : « Suite », les options d'un choix, ou les conditions de victoire et de défaite. On tire un lien d'une sortie vers une étape ou une fin (victoire, défaite) pour créer un embranchement. On glisse le bout d'un lien pour le changer, ou on le sélectionne puis Suppr ;
  - à côté, la **fiche** de l'étape choisie : scène (titre, texte lu, suite ou choix des joueurs avec ses options) ou rencontre (nom, introduction, niveau de loot, type de lieu, conditions de fin avec récompense et suite, règles spéciales). Les textes s'écrivent dans la langue de l'interface, et les autres langues sont gardées ;
  - l'**éditeur de carte** d'une rencontre (« Éditer la carte » sur sa fiche) : une palette (tuiles, jetons de plateau par couleur **et par image** — les variantes, comme les Dangerous version grotte de BE1, se trouvent par les mots de leur fichier, « hole », « eggs » —, filtres par catégorie et par pack, ennemis par famille, zones ; la fiche d'un jeton posé permet d'en changer l'image), la grille au centre et la fiche de la pièce choisie à droite. On choisit une pièce puis on touche une case pour la poser ; on glisse une pièce posée pour la déplacer ; les zones (départ, apparition, nommée) se tracent à la souris. Rotation des tuiles, jetons longs tournés, chevauchements de tuiles signalés en rouge, zones de **brouillard** (une tuile y est rattachée depuis sa fiche ; ses jetons suivent), départ des aventuriers. Clavier : flèches, R, Suppr, Échap. Logique dans `lib/map.ts` (testée) ; les ennemis posés forment la mise en place (`setup.combatants`, carte choisie selon le niveau de loot) ;
  - la fiche d'une rencontre a trois onglets : **Fiche**, **Jetons** (pour chaque jeton à activer posé sur la carte : mode d'activation, consigne lue au MJ, effets avec texte caché et actions) et **Événements** (début de rencontre, début et fin de round, ennemi ou aventurier vaincu, zone révélée, règle cachée lue). Les actions courantes (texte, révéler une zone, poser ou retirer un jeton, faire apparaître des ennemis sur ou à côté d'un jeton, Urgence, or, XP, objet, Restauration, drapeau, fin de rencontre, étape suivante) ont un formulaire ; les autres (choix, tests de compétence, branches imbriquées) et les conditions s'éditent en JSON. Logique dans `lib/events.ts` (testée) ;
  - **schémas** (diagrammes) d'une rencontre, sur sa fiche : ajouter, titrer (« Schéma 1 »), choisir ceux déjà sur la table (`builtOn`), règles spéciales, retirer ; « Carte du schéma » ouvre l'éditeur de carte sur ses seules pièces, le plateau de la rencontre et des schémas sous-jacents estompé dessous (`lib/diagrams.ts`). L'action « Poser un schéma » (`placeDiagram`) le pose en cours de rencontre, « NAVIGATE » (`navigate`) est écrit pour remplacer le plateau par lui (⚠️ le moteur ne l'applique pas encore : il l'affiche au MJ ; de même, `replacesTerrain` n'est pas encore appliqué à la pose, voir docs/design/roadmap.md §8.1) ; l'interrupteur « Son terrain remplace celui déjà posé » règle `replacesTerrain` ;
  - les règles cachées d'un scénario dupliqué sont conservées telles quelles (pas encore éditables) ;
  - vérification en direct par l'API (pastille « Aucun problème » ou nombre de problèmes, signalés sur les étapes ; les avertissements, qui n'empêchent pas de jouer, ont leur propre pastille et s'affichent sur la fiche de l'étape), « Enregistrer » (ou Ctrl+S), puis « Tester », qui enregistre et lance une partie de ce scénario ;
  - la logique du graphe (sorties, liens, choix, mise en page par colonnes) est dans `lib/graph.ts`, testée ; la position de chaque étape est gardée dans le champ `editor` du scénario ;
  - **Impression** (« Imprimer » dans l'éditeur, sur chaque carte de la liste, ou l'icône d'impression d'une rencontre ; `/workshop/print?scenario=<id>`), officiels compris :
    - rencontre au format **Lettre**, mise en page des livrets officiels (calquée sur *The Cave Sickle Queen*, fonds et calques de `assets/templates/encounter/`). **Feuille de mise en place** : bandeau rouge avec le nom et le type de lieu, introduction en italique avec la flèche du nord et le niveau de butin, diagramme aussi grand que la feuille le permet (tuiles, jetons, figurines), dessiné comme les livres : chaque terrain encadré de la couleur de sa sorte (rouge Obstructing, gris Dangerous, ambre Bug Creep, bleu pour l'eau…, `terrainFrame`), chaque jeton de couleur cerclé de sa couleur, le terrain sous les jetons ; une pièce sans image (jetons BE1, pas encore découpés des planches) est dessinée quand même : cadre teinté, ou pastille de sa couleur avec son nom, encadré « Mise en place » en 3 colonnes, écrit comme les livres : tuiles dans l'ordre des numéros, terrain par sorte avec sa taille du catalogue (« 7 × Obstructing (2x1) », « Water » pour « Terrain Light Water »), couleurs dans l'ordre bleu, rouge, vert, jaune, orange, violet ; les groupes restent entiers si possible, sinon un groupe trop long continue dans la colonne suivante (« Terrain (suite) »). Polices aux tailles des livres (titre 26 pt, intro 12 pt, texte 9 pt, intertitres 12 pt). Une carte de plus de 4 tuiles de large est coupée en deux au bord d'une tuile et imprimée sur deux feuilles à la même échelle (« Grande rencontre ») ; la flèche du nord et le niveau de butin passent alors sur la 2ᵉ feuille, l'intro prend toute la largeur. **Feuilles « suite »** : conditions de fin (la 2ᵉ victoire est « alternative ») avec récompense et suite, règles spéciales, puis butin, totems, objectifs… avec l'image du jeton ; chaque texte caché est lu dans tout l'événement (texte caché, puis chaque option d'un choix, réussite, réussite magistrale, échec) et chaque branche a sa case rouge précédée de sa marque en clair (« A: », « PASS: », « MASTERFUL PASS: »…), même quand le JSON les a écrites dans un seul texte ; les événements liés à aucun jeton (ennemi vaincu, début de round…) sont imprimés sous « Autres textes cachés », sauf si une règle les imprime déjà ; en 3 colonnes où le texte coule d'une colonne à l'autre, sur autant de feuilles qu'il faut (« Suite ▶ » en bas). Technique : le texte est mis en colonnes une seule fois, débordant vers la droite ; chaque feuille en montre trois (`EncounterPage.vue`) ;
    - textes cachés **sous le filtre rouge** (bruit rouge du modèle, texte bleuté) ou lisibles (copie du MJ) ;
    - **livret** complet : couverture, étapes numérotées §1, §2… dans l'ordre du fichier, le départ en tête, **paginé comme les livres** : la couverture (page 1) porte le sommaire avec les pages, chaque étape commence sur une nouvelle feuille, chaque feuille porte son numéro au coin extérieur (à gauche sur les pages paires) et le fond de son côté (`print-background-left.jpg` sur les pages paires, `-right` sur les impaires, l'ornement vers l'extérieur : `lib/pageArt.ts`), et les suites renvoient à la page (« Suite : Damp Depths, p. 10 »). Une scène (une histoire) s'imprime comme les histoires des livres (`ScenePage.vue`) : bandeau rouge, deux colonnes justifiées, un alinéa par paragraphe (une ligne du texte), « Suite ▶ » et « (suite) » d'une feuille à l'autre, le renvoi dans un encadré final. Les **schémas** suivent les étapes, un par page, dans l'ordre des numéros (`DiagramPage.vue`) : bandeau vert « Diagram 3 », flèche du nord, puis, comme les livres, seulement l'ajout : en gris les tuiles déjà sur la table (rencontre et schémas de `builtOn`) que ses nouvelles pièces occupent ou touchent par un bord, avec ce qui y est posé, et en couleur ce qui est nouveau (`diagramContext`), une case ne dépassant pas 0,55 pouce, son encadré « Mise en place » (ses seules pièces) et ses règles spéciales ; une rencontre imprimée seule est suivie des siens. Sur toutes les cartes, le noir n'est que sous les tuiles et les terrains hors tuiles (un cadre) : autour d'un plateau en L, c'est le fond de la page. **Poids du PDF** : les JPEG des cartes (tuiles, jetons) sont réencodés en qualité 0,75 dans le navigateur avant l'impression (`lib/lightImage.ts`, attribut `data-images-ready` attendu par `npm run pdf`), le grisé des schémas passe par des modes de fusion (pas de filtre CSS, qui serait rastérisé) : le livret BE1 fait 6 Mo. Chaque rencontre et chaque scène annonce son nombre de feuilles une fois mesurée ; quand tout est compté, `.sheets` prend l'attribut `data-paged`, que `npm run pdf` attend ; **cartes** des textes cachés (41 × 63 mm, 16 par feuille) ;
    - « Télécharger le PDF » le fait fabriquer par l'API (`GET /api/print/pdf?scenario=…&node=…&what=cards&veiled=0`) : Edge (ou Chrome) sans fenêtre, `--print-to-pdf` sur la page imprimable, puis le fichier arrive **entier** (au lieu du fichier à 0 Ko qui se remplit d'« Enregistrer au format PDF »). Navigateur trouvé à sa place habituelle, sinon `Print:BrowserPath` ; n'accepte qu'une demande venue de l'appli sur ce poste (en-tête Referer en localhost) ;
    - « Imprimer » ouvre la boîte de dialogue du navigateur (« Enregistrer au format PDF ») ; sans boîte de dialogue : `npm run pdf -- <id du scénario> [fichier.pdf] [--node <rencontre>] [--cards] [--clear] [--base http://localhost:5173]`, l'appli lancée ;
    - polices : Cinzel et Optima ne sont pas installées (pas de téléchargement) ; la page prend **Palatino Linotype** (titres en petites capitales, introduction en italique) et **Candara** (texte), présentes sous Windows. Le PDF pèse environ 40 Mo pour Blight (fonds de page en 300 dpi) ;
    - logique dans `lib/printable.ts` (testée).
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

Les scénarios de l'atelier (`homebrew:scenario/<nom>`) sont dans `scenarios/homebrew/` (`Content:HomebrewDirectory`). Un fichier de scénario illisible est ignoré (avertissement dans le journal de l'API) au lieu de bloquer toute la liste. Ce sont les seuls que l'API écrit (`PUT`) ou supprime (`DELETE /api/scenarios/homebrew/{nom}`) ; les scénarios officiels restent en lecture seule.

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
