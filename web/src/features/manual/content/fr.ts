import type { Manual } from '../lib/manual'

/** Keep in step with en.ts (manual.test.ts). No spoiler: the GM may read it before playing. */
export const fr: Manual = {
  groups: [
    {
      title: 'Pour commencer',
      sections: [
        {
          id: 'overview',
          title: 'Ce que Taleweaver vous apporte',
          lead: 'Taleweaver seconde le maître du jeu : il tient les comptes, déroule le scénario et fait jouer les ennemis. Vous gardez le récit et le dernier mot.',
          parts: [
            {
              title: 'L’appli s’occupe de',
              list: [
                'L’histoire : textes à lire, choix, récompenses, enchaînement des étapes.',
                'Le combat : initiative, rounds, Urgence, PV, effets, événements et IA des ennemis.',
                'Le plateau : mise en place, zones cachées, figurines.',
                'La campagne : or, XP, disciplines, objets, boutique et sauvegarde automatique.',
                'Les règles : une recherche dans le livre, en pleine partie.',
              ],
            },
            {
              title: 'La table garde',
              list: [
                'Le jeu physique : plateau, figurines, cartes et dés. L’appli ne lance pas de dés.',
                'Les réponses : l’appli ne voit pas la table, elle vous demande ce qui s’y passe.',
                'L’arbitrage : ses suggestions se corrigent, et en cas de doute le livre et vous l’emportez.',
              ],
            },
            {
              title: 'Par où commencer ?',
              figure: {
                image: 'home',
                caption: 'L’accueil.',
                legend: [
                  'Lancer un scénario : [[home.play]].',
                  'Reprendre une partie, là où elle s’est arrêtée : [[home.resume]].',
                  'Écrire ses propres aventures : [[#workshop]].',
                  'Chercher une règle : [[#rules]].',
                  'Cette aide, ouverte à la section de la page où vous êtes.',
                ],
              },
              list: [
                'Vous organisez une soirée : [[#evening]].',
                'Vous lancez ou reprenez l’Acte 1 : [[#act1]].',
                'Un joueur arrive, part ou manque : [[#players]].',
                'Vous voulez écrire votre aventure : [[#workshop]].',
              ],
              note: 'Les téléphones des joueurs sont facultatifs : tout peut se faire depuis votre écran.',
            },
          ],
        },
        {
          id: 'evening',
          title: 'Organiser une soirée',
          lead: 'La première fois, essayez seul un court scénario pendant quelques tours, puis supprimez cette partie.',
          parts: [
            {
              title: 'Avant l’arrivée des joueurs',
              steps: [
                'Sur l’accueil, choisissez un scénario avec [[home.play]], ou une partie avec [[home.resume]].',
                'Pour une nouvelle partie, réglez si besoin [[options.button]] (variantes et difficulté), puis [[start.prepare]]. Ces options ne se changent plus ensuite.',
                'Si les joueurs auront leur téléphone, ouvrez [[room.gm.button]] et vérifiez qu’un téléphone du même Wi-Fi la rejoint.',
                'Pour jouer sur une tablette, ou montrer la carte aux joueurs, ouvrez [[screens.title]] dans le bandeau de la partie.',
                'Pour une reprise, ouvrez [[comfort.tools.session]] dans le menu ☰ : [[comfort.session.tab.next]] donne le matériel à sortir, mais révèle la suite.',
              ],
            },
            {
              title: 'Au début de la soirée',
              steps: [
                'Nouvelle partie : composez le groupe avec les joueurs, puis [[preparation.setOut]].',
                'Reprise : lisez [[comfort.session.tab.summary]] dans [[comfort.tools.session]], puis notez qui est là dans [[comfort.session.tab.players]].',
                'Présentez la vue joueur à ceux qui ont un téléphone.',
              ],
            },
            {
              title: 'Pendant la soirée',
              steps: [
                'Lisez le récit et répondez aux questions de l’appli : [[#story]].',
                'En rencontre, installez le plateau puis menez les tours : [[#encounter]].',
                'Pour une interruption, [[comfort.tools.pause]] affiche un écran neutre et suspend les demandes des téléphones.',
              ],
            },
            {
              title: 'En fin de soirée',
              steps: [
                'Laissez une note dans [[comfort.session.tab.summary]] pour la prochaine fois.',
                'Suivez [[comfort.session.tab.storage]] : il dit quelles cartes vont avec chaque aventurier et ce qu’il faut conserver.',
                'Fermez la room, puis la page. Il n’y a rien à enregistrer.',
              ],
            },
          ],
        },
        {
          id: 'act1',
          title: 'Lancer ou reprendre l’Acte 1',
          lead: 'L’Acte 1 n’est pas encore complet dans l’appli : seul son début s’y joue, les pages 4 à 31 du livre d’aventure.',
          parts: [
            {
              title: 'Commencer l’acte',
              steps: [
                'Lancez l’Acte 1 depuis l’accueil. L’histoire impose quatre aventuriers et leur paquetage de départ.',
                'Répartissez-les entre les joueurs. À moins de quatre, un joueur en prend plusieurs, ou vous en jouez.',
                'Choisissez [[preparation.setOut]] : le récit commence.',
              ],
            },
            {
              title: 'Continuer une partie de l’appli',
              text: 'Retrouvez-la dans [[home.games]] et choisissez [[home.resume]]. Elle reprend où vous l’aviez laissée, même au milieu d’un combat. Si les joueurs ont changé, voyez [[#players]].',
            },
            {
              title: 'Reprendre une campagne commencée sans l’appli',
              text: 'C’est possible si le groupe en est encore dans les pages 4 à 31. Il faut reporter l’état de la campagne à la main :',
              steps: [
                'Lancez l’Acte 1 et faites partir le groupe.',
                'Menu ☰, [[comfort.tools.correct]], [[comfort.correct.field.node]] : choisissez l’étape où vous en êtes. La liste montre le titre des étapes suivantes.',
                'Avec ce même outil, reportez [[comfort.correct.field.gold]], l’XP totale de chaque aventurier et les [[sheet.flags]] de la feuille d’aventure. L’étape choisie peut avoir donné des gains : faites-le après y être allé.',
                'Dans [[inventory.open]], ajoutez les objets gagnés et retirez ceux qui ne sont plus là.',
                'Reportez les disciplines : dans [[storyRound.training]] en phase d’histoire, ou dans [[room.gm.button]] avec [[room.gm.openAsPlayer]], sur la fiche de l’aventurier.',
              ],
              note: 'Aller à une étape ne rejoue pas celles d’avant : leurs gains et leurs choix sont à reporter.',
            },
            {
              title: 'Après la page 31',
              text: 'Continuez avec le livre. La recherche de règles reste utilisable et vous pouvez recréer une rencontre dans [[#workshop]], mais cette partie ne suivra pas la suite de l’acte.',
            },
          ],
        },
        {
          id: 'players',
          title: 'Joueurs qui arrivent, partent ou manquent',
          lead: 'Les joueurs et les aventuriers sont distincts : un aventurier garde sa progression quand il change de joueur.',
          parts: [
            {
              title: 'Un nouveau joueur en cours de campagne',
              steps: [
                'Résumez l’histoire avec [[comfort.session.tab.summary]] et le [[play.journal]].',
                'Choisissez avec la table l’aventurier du groupe qu’il reprend.',
                'Inscrivez son nom dans [[comfort.session.tab.players]] et enregistrez.',
                'S’il a un téléphone, il rejoint la room et vous lui donnez l’aventurier avec [[room.gm.giveAdventurers]].',
                'Passez en revue avec lui la carte, l’équipement et les disciplines de l’aventurier.',
              ],
              note: 'On ne crée pas d’aventurier en cours de partie. Le groupe ne s’agrandit que lorsque le scénario le prévoit.',
            },
            {
              title: 'Un joueur absent',
              text: 'Dans [[comfort.session.tab.players]], décochez [[comfort.players.present]] et indiquez qui le remplace. Cela ne change pas les téléphones : confiez aussi l’aventurier au remplaçant dans la room.',
            },
            {
              title: 'Un joueur qui part pour de bon',
              text: 'Retirez-le avec [[room.gm.remove]]. Son aventurier reste dans le groupe : confiez-le à quelqu’un ou jouez-le.',
            },
            {
              title: 'Un nouvel aventurier amené par l’histoire',
              text: 'Il arrive avec le scénario. Selon le cas, l’appli vous guide pour ses disciplines, ses objets et son joueur. Si plus de quatre aventuriers sont disponibles, choisissez ceux qui jouent les rencontres ([[storyRound.activeGroup]]).',
            },
          ],
        },
      ],
    },
    {
      title: 'Les fonctionnalités',
      sections: [
        {
          id: 'preparation',
          title: 'Préparer le groupe',
          lead: 'Chaque scénario fixe ses règles de départ : aventuriers permis ou imposés, taille du groupe, or, XP et paquetages.',
          parts: [
            {
              figure: {
                image: 'party',
                caption: 'La préparation du groupe, avant le départ.',
                legend: [
                  'Un aventurier choisi : la croix le retire.',
                  'Un aventurier sans carte, sous le nom de votre choix, si le scénario le permet.',
                  '[[preparation.openRoom]] : chacun choisit sur son téléphone.',
                  '[[preparation.setOut]] : le récit commence.',
                  'Le bandeau du groupe : PV et XP de chacun, toujours visibles pendant la partie.',
                ],
              },
            },
            {
              list: [
                'Choisissez vous-même, ou utilisez [[preparation.openRoom]] pour que chacun le fasse sur son téléphone.',
                'Selon le scénario, chacun prend un paquetage, achète des cartes et apprend des disciplines. L’appli compte le budget.',
                'Certains scénarios acceptent un aventurier sans carte, sous le nom de votre choix.',
                'Si [[preparation.setOut]] est grisé, le message au-dessus du groupe dit ce qui manque.',
              ],
              note: '[[preparation.showHidden]] révèle des surprises de l’histoire, aussi sur les téléphones.',
            },
          ],
        },
        {
          id: 'story',
          title: 'Histoire, boutique et progression',
          lead: 'Entre deux rencontres, la partie se lit comme le livre.',
          parts: [
            {
              title: 'Le récit',
              figure: {
                image: 'story',
                caption: 'Une scène du récit.',
                legend: [
                  'Le texte à lire à la table.',
                  'Le bouton qui fait avancer l’histoire ; à un choix, une réponse par sortie.',
                ],
              },
              list: [
                '[[play.story]] montre la page à lire et la question en cours. Votre réponse fait avancer l’histoire.',
                '[[comfort.tools.reading]], dans le menu ☰, affiche le texte en grand pour la lecture à voix haute.',
                '[[play.journal]] garde tout ce qui s’est passé.',
                '[[sheet.open]] rassemble les drapeaux de la campagne et les cartes cachées déjà révélées.',
              ],
            },
            {
              title: 'La phase d’histoire',
              list: [
                '[[storyRound.phase.shop]] : achats, ventes, échanges, améliorations et disciplines.',
                '[[storyRound.phase.explore]] : quêtes annexes et bounties permises. L’appli vous prévient des consommables qui seront défaussés.',
                '[[storyRound.phase.ventureForth]] : le groupe quitte cette phase et poursuit l’aventure.',
              ],
            },
            {
              title: 'L’inventaire',
              text: '[[inventory.open]] s’ouvre à tout moment après le départ : équiper, donner, utiliser, vendre, ajouter une carte. Un badge signale le butin à distribuer. Pour un objet « à tirer », piochez la vraie carte, puis choisissez-la dans l’appli.',
            },
          ],
        },
        {
          id: 'encounter',
          title: 'Mener une rencontre',
          lead: 'Le plateau est au centre, le tour en cours à côté, l’initiative et l’Urgence en haut.',
          parts: [
            {
              title: 'Installer',
              figure: {
                image: 'setup',
                caption: 'La mise en place d’une rencontre.',
                legend: [
                  'Le plateau à reproduire : tuiles, jetons, ennemis et zone de départ.',
                  'Ce qui reste à faire avant de commencer ; [[comfort.ready.confirm]] le confirme.',
                  '[[table.showTable]] : la carte seule, en plein écran ou sur un second moniteur.',
                  'Les onglets : combattants, journal, et [[play.tabEncounter]] pour les objectifs et règles spéciales.',
                  '[[play.startRound]].',
                ],
              },
              steps: [
                'Reproduisez le plateau affiché. Pour cocher le matériel, ouvrez [[comfort.session.tab.material]] dans [[comfort.tools.session]].',
                'Placez les aventuriers et lisez les règles spéciales, dans l’onglet [[play.tabEncounter]].',
                'Choisissez [[play.startRound]].',
              ],
            },
            {
              title: 'Au tour d’un aventurier',
              figure: {
                image: 'adventurer-turn',
                caption: 'Le tour d’un aventurier.',
                legend: [
                  'La piste d’initiative : qui joue, et qui suit.',
                  'Ce que l’appli attend de vous maintenant.',
                  'Les actions de l’aventurier et leur coût en SP, à cocher au fil du tour.',
                  '[[play.nextTurn]], une fois le tour joué à la table.',
                ],
              },
              steps: [
                'Le joueur joue à table.',
                'Touchez une figurine pour noter dégâts, soins, effets et SP, ou pour ouvrir [[duel.open]].',
                'S’il active un jeton ou entre dans une zone cachée, touchez-les sur le plateau pour les activer ou les révéler.',
                'Choisissez [[play.nextTurn]].',
              ],
            },
            {
              title: 'Au tour des ennemis',
              figure: {
                image: 'enemy-turn',
                caption: 'Le tour d’un ennemi : l’appli lit sa carte d’IA.',
                legend: [
                  'La condition de l’étape en cours, à vérifier sur la table.',
                  'La suggestion de l’appli, d’après les positions sur le plateau.',
                  'Votre réponse : l’appli donne alors l’instruction à exécuter.',
                ],
              },
              steps: [
                'L’appli lit leur carte et vous pose chaque condition. Répondez d’après la table.',
                'Si les positions sont suivies, elle suggère la réponse, la cible et le déplacement. Vérifiez avant d’accepter.',
                'Exécutez l’instruction et résolvez l’attaque, puis passez au tour suivant.',
              ],
            },
            {
              title: 'Résoudre une attaque ou un sort',
              steps: [
                'Dans [[duel.title]], vérifiez l’attaquant, la cible, les dés et la situation.',
                'Lancez les vrais dés et touchez les faces obtenues, ou utilisez [[duel.manual]].',
                'Les capacités de symbole des armes équipées sont proposées comme celles des ennemis ; un COMBO dit s’il est actif.',
                'Validez le résultat proposé : dégâts, raté ou résistance sont enregistrés en un geste.',
              ],
            },
            {
              title: 'Terminer',
              figure: {
                image: 'game-bar',
                caption: 'Le bandeau d’une partie, de gauche à droite.',
                legend: [
                  '[[play.undo]] : défait la dernière action.',
                  '[[inventory.open]] : objets, équipement et butin à distribuer.',
                  '[[room.gm.button]] : les téléphones des joueurs.',
                  '[[screens.title]] : une tablette pour mener, ou un écran pour la table.',
                  '[[sheet.open]] : drapeaux de la campagne et cartes révélées.',
                  '[[reference.open]] : la recherche de règles.',
                  'Le menu ☰ : session, lecture, corrections et autres outils du MJ.',
                ],
              },
              text: 'L’onglet [[play.tabEncounter]] contient les objectifs, les conditions de fin, les succès et les compteurs. L’appli propose certaines victoires, sans les appliquer : les autres sont à déclarer vous-même. Les récompenses et le récit suivent.',
              note: 'Si un bouton est grisé, une question attend souvent votre réponse dans le panneau du tour.',
            },
          ],
        },
        {
          id: 'table',
          title: 'Le plateau et les écrans',
          lead: 'La carte sert à installer la rencontre et, si vous le souhaitez, à suivre les déplacements.',
          parts: [
            {
              list: [
                'Glissez une figurine, ou touchez-la puis touchez une case. C’est facultatif, mais les suggestions de l’IA en dépendent.',
                '[[table.showTable]] met la carte en plein écran ou l’ouvre dans une autre fenêtre, pour un second moniteur.',
                'Pour mener depuis une tablette : [[screens.title]], [[screens.gm.show]], puis scannez-le avec elle. Le PC reste allumé et garde ses droits.',
                'Pour un écran tourné vers les joueurs : [[screens.title]], [[screens.table.show]]. Il montre sans rien permettre.',
                'Un QR code sert une fois. Suppression, export, atelier, room et QR codes restent sur le PC.',
                'En HTTP, une tablette peut se mettre en veille : allongez son délai de veille. Le Wi-Fi doit être de confiance.',
              ],
              note: 'La tablette du MJ et la seconde fenêtre du PC ont vos droits : ce qu’on y touche est appliqué. Ne laissez aux joueurs que l’écran de table.',
            },
          ],
        },
        {
          id: 'room',
          title: 'La room des joueurs',
          lead: 'Elle relie les téléphones à la partie, pour que chacun suive son aventurier et vous décharge d’une partie des saisies.',
          parts: [
            {
              title: 'La mettre en place',
              figure: {
                image: 'room',
                caption: 'La room ouverte.',
                legend: [
                  'Le QR code à scanner, avec l’adresse et le code à taper à défaut.',
                  '[[room.gm.share]] : un aventurier à chaque joueur qui n’en a pas.',
                  'Les demandes des joueurs, à accepter ou refuser.',
                  '[[room.gm.openAsPlayer]] : ce que voit le joueur de cet aventurier.',
                ],
              },
              steps: [
                'Dans la partie, ouvrez [[room.gm.button]] puis [[room.gm.open]].',
                'Les joueurs, sur le même Wi-Fi, scannent le QR code ou tapent l’adresse et le code. La room accepte huit joueurs.',
                'Confiez les aventuriers un par un, ou utilisez [[room.gm.share]] pour en donner un à chaque joueur qui n’en a pas.',
              ],
            },
            {
              title: 'Garder la main',
              list: [
                'Les demandes des joueurs s’affichent dans la room, avec un badge sur son bouton. Acceptez, refusez ou acceptez tout.',
                'Vous pouvez les laisser gérer leurs objets hors rencontre, ou accepter d’office certains types de demandes, joueur par joueur.',
                'Les SP et les cartes épuisées cochés par un joueur s’appliquent sans validation.',
                '[[room.gm.show]] envoie une carte sur les téléphones.',
                '[[room.gm.openAsPlayer]] vous montre ce que voit un joueur : utile pour lui expliquer sa vue.',
              ],
              note: 'Les téléphones ne reçoivent pas les secrets du scénario. Votre écran en montre : ne le tournez pas vers la table.',
            },
          ],
        },
        {
          id: 'tools',
          title: 'Corriger et gagner du temps',
          lead: 'Une erreur de saisie se rattrape. Les outils du MJ sont dans le menu ☰ de la partie.',
          parts: [
            {
              list: [
                '[[play.undo]] (la flèche du bandeau) défait la dernière action, y compris celle d’un joueur.',
                '[[comfort.revert.line]], dans le journal, revient plus loin en montrant d’abord ce qui sera perdu.',
                '[[comfort.tools.correct]] modifie l’or, l’XP, un drapeau, l’étape et, en combat, l’Urgence, le round, la piste ou les apparitions. Les conséquences de la nouvelle valeur ne sont pas jouées.',
                '[[comfort.tools.several]] applique dégâts, soins ou effet à plusieurs figurines à la fois.',
                '[[comfort.tools.session]] réunit résumé, notes, présents, matériel, arbitrages et rangement.',
              ],
              note: 'Après une mise à jour de l’appli, les actions plus anciennes ne peuvent pas toujours être annulées.',
            },
          ],
        },
        {
          id: 'rules',
          title: 'Retrouver une règle',
          lead: 'La recherche comprend les questions en français. Le texte du livre, lui, est en anglais.',
          parts: [
            {
              list: [
                'Ctrl+K ouvre les règles par-dessus l’écran en cours. Le bouton « ? » d’une partie et le menu [[reference.open]] y mènent aussi.',
                'Tapez une question ou un mot, comme « ligne de vue », ou parcourez questions fréquentes, mots-clés et sommaire.',
                'Pour garder une décision de la table, notez-la dans [[comfort.session.tab.rulings]], avec sa page.',
              ],
            },
          ],
        },
        {
          id: 'workshop',
          title: 'Créer ses aventures',
          lead: 'L’atelier crée des scénarios maison, sans toucher aux scénarios officiels.',
          parts: [
            {
              figure: {
                image: 'workshop',
                caption: 'L’atelier.',
                legend: [
                  '[[workshop.sharing.import]] : un scénario partagé par un autre MJ.',
                  '[[workshop.new]] : un scénario vierge.',
                  '[[workshop.duplicate]] : partir d’un scénario existant, sans toucher à l’original.',
                ],
              },
            },
            {
              figure: {
                image: 'editor',
                caption: 'L’éditeur d’un scénario.',
                legend: [
                  'Le graphe : chaque étape (scène, rencontre, phase d’histoire) et ses sorties. Choisissez-en une pour remplir sa fiche à droite.',
                ],
              },
            },
            {
              steps: [
                'Dans [[app.workshop]], choisissez [[workshop.new]], ou [[workshop.duplicate]] pour partir d’un scénario officiel.',
                'Reliez scènes, rencontres et phases d’histoire sur le graphe, puis remplissez leur fiche.',
                'Dessinez la carte de chaque rencontre avec [[workshop.map.open]] : tuiles, jetons, ennemis, départ et zones cachées.',
                'Ajoutez événements, textes cachés et règles de départ du groupe.',
                'Corrigez les problèmes signalés, puis [[workshop.test]]. Cet essai crée une partie, à supprimer ensuite depuis l’accueil.',
                'Pour voir une rencontre telle qu’imprimée sans quitter sa fiche, ouvrez [[workshop.preview.open]].',
                'Au besoin, imprimez le livret ou téléchargez-le en PDF.',
                'Pour partager un scénario, exportez-le depuis sa carte (icône de téléchargement) : l’autre MJ choisit [[workshop.sharing.import]] dans son atelier. Le scénario importé arrive à côté des siens, sous un nom libre ; il lui faut les mêmes packs de contenu.',
              ],
              note: 'Dupliquer ou imprimer un scénario officiel en révèle le contenu. Ces copies restent à usage personnel.',
            },
          ],
        },
      ],
    },
    {
      title: 'En cas de besoin',
      sections: [
        {
          id: 'help',
          title: 'Sauvegardes et dépannage',
          lead: 'Chaque action est enregistrée aussitôt sur le PC du MJ.',
          parts: [
            {
              title: 'Vos parties',
              list: [
                'Elles sont listées sur l’accueil. La corbeille les supprime définitivement.',
                'Leurs fichiers sont par défaut dans %APPDATA%\\Middara\\saves : copiez ce dossier pour les sauvegarder.',
                'Avec Taleweaver.exe, vos scénarios sont dans %APPDATA%\\Middara\\scenarios. Sous Linux, parties et scénarios sont dans ~/.config/Middara. Remplacer l’appli par une version plus récente les garde.',
                'À la fin d’une aventure, vous pouvez reprendre à la dernière rencontre ou recommencer. La partie terminée est gardée.',
              ],
            },
            {
              title: 'Un téléphone ne rejoint pas la room',
              list: [
                'Vérifiez qu’il est sur le même Wi-Fi, pas en données mobiles, et que la room est ouverte.',
                'Autorisez l’appli dans le pare-feu Windows, pour les réseaux privés (il l’affiche sous le nom de Taleweaver, ou de Node.js si vous la lancez depuis le code).',
                'Si plusieurs adresses sont proposées dans la room, essayez-en une autre. Coupez un éventuel VPN.',
                'Le PC du MJ doit rester allumé, avec l’appli lancée.',
              ],
            },
            {
              title: 'L’appli se trompe ou bloque',
              list: [
                'Cherchez d’abord une question en attente, dans le panneau du tour ou sous le récit.',
                'Annulez, ou utilisez [[comfort.tools.correct]].',
                'Si une page affiche une erreur de chargement, vérifiez que le serveur de l’appli tourne, puis rechargez-la. La partie est intacte.',
                'En dernier recours, jouez le point litigieux avec le livre, puis reportez le résultat.',
              ],
            },
          ],
        },
      ],
    },
  ],
}
