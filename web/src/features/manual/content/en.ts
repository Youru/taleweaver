import type { Manual } from '../lib/manual'

/** Keep in step with fr.ts (manual.test.ts). No spoiler: the GM may read it before playing. */
export const en: Manual = {
  groups: [
    {
      title: 'Getting started',
      sections: [
        {
          id: 'overview',
          title: 'What Taleweaver brings you',
          lead: 'Taleweaver assists the Game Master: it keeps count, runs the scenario and plays the enemies. The story and the last word stay yours.',
          parts: [
            {
              title: 'The app handles',
              list: [
                'The story: texts to read, choices, rewards and what comes next.',
                'The fight: initiative, rounds, Urgency, HP, effects, events and the enemies’ AI.',
                'The board: setup, hidden areas and figures.',
                'The campaign: Gold, XP, Disciplines, items, Store and automatic saving.',
                'The rules: a search through the rulebook, during the game.',
              ],
            },
            {
              title: 'The table keeps',
              list: [
                'The physical game: board, figures, cards and dice. The app rolls no dice.',
                'The answers: the app cannot see the table, so it asks you what happens there.',
                'The rulings: its suggestions can be corrected, and when in doubt the book and you prevail.',
              ],
            },
            {
              title: 'Where to start?',
              list: [
                'You are hosting an evening: [[#evening]].',
                'You are starting or resuming Act 1: [[#act1]].',
                'A player joins, leaves or is away: [[#players]].',
                'You want to write your own adventure: [[#workshop]].',
              ],
              note: 'The players’ phones are optional: everything can be done from your screen.',
            },
          ],
        },
        {
          id: 'evening',
          title: 'Hosting an evening',
          lead: 'The first time, try a short scenario on your own for a few turns, then delete that game.',
          parts: [
            {
              title: 'Before the players arrive',
              steps: [
                'On the home page, pick a scenario with [[home.play]], or a game with [[home.resume]].',
                'For a new game, set [[options.button]] if needed (variants and difficulty), then [[start.prepare]]. These options cannot be changed later.',
                'If the players will use phones, open [[room.gm.button]] and check that a phone on the same Wi-Fi can join.',
                'To play on a tablet, or show the map to the players, open [[screens.title]] in the game’s banner.',
                'When resuming, open [[comfort.tools.session]] from the ☰ menu: [[comfort.session.tab.next]] lists the material to take out, but reveals what comes next.',
              ],
            },
            {
              title: 'When the evening starts',
              steps: [
                'New game: build the party with the players, then [[preparation.setOut]].',
                'Resumed game: read [[comfort.session.tab.summary]] in [[comfort.tools.session]], then note who is here in [[comfort.session.tab.players]].',
                'Show the player view to those who have a phone.',
              ],
            },
            {
              title: 'During the evening',
              steps: [
                'Read the story and answer the app’s questions: [[#story]].',
                'In an encounter, set up the board and run the turns: [[#encounter]].',
                'For a break, [[comfort.tools.pause]] shows a neutral screen and holds the phones’ requests.',
              ],
            },
            {
              title: 'When the evening ends',
              steps: [
                'Leave a note in [[comfort.session.tab.summary]] for next time.',
                'Follow [[comfort.session.tab.storage]]: it lists each adventurer’s cards and what to keep.',
                'Close the room, then the page. There is nothing to save.',
              ],
            },
          ],
        },
        {
          id: 'act1',
          title: 'Starting or resuming Act 1',
          lead: 'Act 1 is not complete in the app yet: only its beginning can be played, pages 4 to 31 of the Adventure Book.',
          parts: [
            {
              title: 'Starting the Act',
              steps: [
                'Start Act 1 from the home page. The story imposes four adventurers and their starting kits.',
                'Share them between the players. With fewer than four, a player takes several, or you play some.',
                'Choose [[preparation.setOut]]: the story begins.',
              ],
            },
            {
              title: 'Continuing a game of the app',
              text: 'Find it under [[home.games]] and choose [[home.resume]]. It carries on where you left it, even in the middle of a fight. If the players changed, see [[#players]].',
            },
            {
              title: 'Resuming a campaign started without the app',
              text: 'This works if the party is still within pages 4 to 31. The state of the campaign is copied by hand:',
              steps: [
                'Start Act 1 and set the party out.',
                '☰ menu, [[comfort.tools.correct]], [[comfort.correct.field.node]]: pick the step you are at. The list shows the titles of later steps.',
                'With the same tool, enter [[comfort.correct.field.gold]], each adventurer’s total XP and the [[sheet.flags]] of the Adventure Sheet. The step you picked may have given rewards, so do this after going there.',
                'In [[inventory.open]], add the items won and remove those that are gone.',
                'Enter the Disciplines: under [[storyRound.training]] in a Story Round, or in [[room.gm.button]] with [[room.gm.openAsPlayer]], on the adventurer’s sheet.',
              ],
              note: 'Going to a step does not replay the ones before it: their rewards and choices are yours to enter.',
            },
            {
              title: 'After page 31',
              text: 'Carry on with the book. The rules search still works and you can rebuild an encounter in [[#workshop]], but this game will not follow the rest of the Act.',
            },
          ],
        },
        {
          id: 'players',
          title: 'Players who join, leave or are away',
          lead: 'Players and adventurers are separate: an adventurer keeps their progress when the player changes.',
          parts: [
            {
              title: 'A new player during the campaign',
              steps: [
                'Sum up the story with [[comfort.session.tab.summary]] and the [[play.journal]].',
                'Pick, with the table, the adventurer of the party they take over.',
                'Write their name in [[comfort.session.tab.players]] and save.',
                'If they have a phone, they join the room and you hand them the adventurer with [[room.gm.giveAdventurers]].',
                'Go through the adventurer’s card, equipment and Disciplines with them.',
              ],
              note: 'No adventurer can be created during a game. The party only grows when the scenario says so.',
            },
            {
              title: 'A player is away',
              text: 'In [[comfort.session.tab.players]], untick [[comfort.players.present]] and say who stands in. This leaves the phones as they are: also hand the adventurer over in the room.',
            },
            {
              title: 'A player leaves for good',
              text: 'Remove them with [[room.gm.remove]]. Their adventurer stays in the party: hand it to someone or play it yourself.',
            },
            {
              title: 'A new adventurer brought by the story',
              text: 'They arrive with the scenario. Depending on the case, the app guides you through their Disciplines, items and player. With more than four adventurers available, choose who plays the encounters ([[storyRound.activeGroup]]).',
            },
          ],
        },
      ],
    },
    {
      title: 'Features',
      sections: [
        {
          id: 'preparation',
          title: 'Preparing the party',
          lead: 'Each scenario sets its own starting rules: adventurers allowed or required, party size, Gold, XP and kits.',
          parts: [
            {
              list: [
                'Choose yourself, or use [[preparation.openRoom]] so that each player does it on their phone.',
                'Depending on the scenario, each takes a kit, buys cards and learns Disciplines. The app keeps the budget.',
                'Some scenarios accept an adventurer without a card, under any name you like.',
                'If [[preparation.setOut]] is greyed out, the message above the party says what is missing.',
              ],
              note: '[[preparation.showHidden]] reveals surprises of the story, on the phones too.',
            },
          ],
        },
        {
          id: 'story',
          title: 'Story, Store and progress',
          lead: 'Between encounters, the game reads like the book.',
          parts: [
            {
              title: 'The story',
              list: [
                '[[play.story]] shows the page to read and the current question. Your answer moves the story on.',
                '[[comfort.tools.reading]], in the ☰ menu, shows the text large for reading aloud.',
                '[[play.journal]] keeps everything that happened.',
                '[[sheet.open]] gathers the campaign’s flags and the hidden cards revealed so far.',
              ],
            },
            {
              title: 'The Story Round',
              list: [
                '[[storyRound.phase.shop]]: buying, selling, trading, upgrades and Disciplines.',
                '[[storyRound.phase.explore]]: Side Quests and the Bounties allowed. The app warns you of the Consumables to be discarded.',
                '[[storyRound.phase.ventureForth]]: the party leaves this round and continues the adventure.',
              ],
            },
            {
              title: 'The inventory',
              text: '[[inventory.open]] opens at any time after setting out: equip, give, use, sell, add a card. A badge flags loot to hand out. For an item “to draw”, draw the real card, then pick it in the app.',
            },
          ],
        },
        {
          id: 'encounter',
          title: 'Running an encounter',
          lead: 'The board is in the middle, the current turn beside it, initiative and Urgency on top.',
          parts: [
            {
              title: 'Setting up',
              steps: [
                'Build the board as shown. To tick off the material, open [[comfort.session.tab.material]] in [[comfort.tools.session]].',
                'Place the adventurers and read the special rules, in the [[play.tabEncounter]] tab.',
                'Choose [[play.startRound]].',
              ],
            },
            {
              title: 'On an adventurer’s turn',
              steps: [
                'The player plays at the table.',
                'Touch a figure to note damage, healing, effects and SP, or to open [[duel.open]].',
                'If they activate a token or enter a hidden area, touch it on the board to activate or reveal it.',
                'Choose [[play.nextTurn]].',
              ],
            },
            {
              title: 'On the enemies’ turn',
              steps: [
                'The app reads their card and asks you each condition. Answer from the table.',
                'If positions are followed, it suggests the answer, target and move. Check before accepting.',
                'Carry out the instruction and resolve the attack, then go to the next turn.',
              ],
            },
            {
              title: 'Resolving an Attack or a Spell',
              steps: [
                'In [[duel.title]], check the attacker, target, dice and situation.',
                'Roll the real dice and touch the faces rolled, or use [[duel.manual]].',
                'Confirm the proposed result: damage, miss or resistance are recorded in one go.',
              ],
            },
            {
              title: 'Finishing',
              text: 'The [[play.tabEncounter]] tab holds the goals, end conditions, Achievements and counters. The app proposes some victories without applying them: declare the others yourself. Rewards and story follow.',
              note: 'If a button is greyed out, a question is often waiting for your answer in the turn panel.',
            },
          ],
        },
        {
          id: 'table',
          title: 'The board and the screens',
          lead: 'The map serves to set up the encounter and, if you wish, to follow the moves.',
          parts: [
            {
              list: [
                'Drag a figure, or touch it then touch a square. It is optional, but the AI suggestions depend on it.',
                '[[table.showTable]] shows the map full screen or opens it in another window, for a second monitor.',
                'To run the game from a tablet: [[screens.title]], [[screens.gm.show]], then scan it with the tablet. The PC stays on and keeps its rights.',
                'For a screen facing the players: [[screens.title]], [[screens.table.show]]. It shows, and allows nothing.',
                'A QR code works once. Deleting, exporting, the workshop, the room and the QR codes stay on the PC.',
                'Over HTTP a tablet may fall asleep: lengthen its sleep delay. Use a trusted Wi-Fi.',
              ],
              note: 'The GM’s tablet and the PC’s second window have your rights: whatever is touched there applies. Leave only the table screen to the players.',
            },
          ],
        },
        {
          id: 'room',
          title: 'The players’ room',
          lead: 'It links the phones to the game, so that players follow their adventurer and take some of the typing off you.',
          parts: [
            {
              title: 'Setting it up',
              steps: [
                'In the game, open [[room.gm.button]], then [[room.gm.open]].',
                'The players, on the same Wi-Fi, scan the QR code or type the address and code. The room takes eight players.',
                'Hand out the adventurers one by one, or use [[room.gm.share]] to give one to each player without any.',
              ],
            },
            {
              title: 'Staying in control',
              list: [
                'The players’ requests appear in the room, with a badge on its button. Accept, refuse or accept all.',
                'You can let them manage their items outside encounters, or accept some kinds of requests automatically, player by player.',
                'SP and exhausted cards ticked by a player apply without validation.',
                '[[room.gm.show]] sends a card to the phones.',
                '[[room.gm.openAsPlayer]] shows you what a player sees, which helps when explaining their view.',
              ],
              note: 'The phones do not receive the scenario’s secrets. Your screen shows some: keep it turned away from the table.',
            },
          ],
        },
        {
          id: 'tools',
          title: 'Correcting and saving time',
          lead: 'A slip can be put right. The GM’s tools are in the game’s ☰ menu.',
          parts: [
            {
              list: [
                '[[play.undo]] (the arrow of the banner) undoes the last action, a player’s included.',
                '[[comfort.revert.line]], in the journal, goes further back and first shows what will be lost.',
                '[[comfort.tools.correct]] changes Gold, XP, a flag, the step and, in a fight, Urgency, round, track or spawns. What the new value would trigger is not played.',
                '[[comfort.tools.several]] applies damage, healing or an effect to several figures at once.',
                '[[comfort.tools.session]] gathers summary, notes, attendance, material, rulings and storage.',
              ],
              note: 'After an update of the app, older actions cannot always be undone.',
            },
          ],
        },
        {
          id: 'rules',
          title: 'Finding a rule',
          lead: 'The search understands questions in French and English. The book’s text is in English.',
          parts: [
            {
              list: [
                'Ctrl+K opens the rules over the current screen. The “?” button of a game and the [[reference.open]] menu lead there too.',
                'Type a question or a word, such as “line of sight”, or browse frequent questions, keywords and contents.',
                'To keep a decision of the table, note it in [[comfort.session.tab.rulings]], with its page.',
              ],
            },
          ],
        },
        {
          id: 'workshop',
          title: 'Creating adventures',
          lead: 'The workshop makes homebrew scenarios and never touches the official ones.',
          parts: [
            {
              steps: [
                'In [[app.workshop]], choose [[workshop.new]], or [[workshop.duplicate]] to start from an official scenario.',
                'Link scenes, encounters and Story Rounds on the graph, then fill in their sheets.',
                'Draw each encounter’s map with [[workshop.map.open]]: tiles, tokens, enemies, start and hidden areas.',
                'Add events, hidden texts and the party’s starting rules.',
                'Fix the problems flagged, then [[workshop.test]]. This creates a game, to delete afterwards from the home page.',
                'If needed, print the booklet or download it as a PDF.',
              ],
              note: 'Duplicating or printing an official scenario reveals its content. Such copies are for personal use only.',
            },
          ],
        },
      ],
    },
    {
      title: 'When you need it',
      sections: [
        {
          id: 'help',
          title: 'Saves and troubleshooting',
          lead: 'Every action is saved at once on the GM’s PC.',
          parts: [
            {
              title: 'Your games',
              list: [
                'They are listed on the home page. The bin deletes them for good.',
                'By default their files are in %APPDATA%\\Middara\\saves: copy that folder to back them up.',
                'With Taleweaver.exe, your scenarios are in %APPDATA%\\Middara\\scenarios. On Linux, games and scenarios are in ~/.config/Middara. Replacing the app with a newer version keeps them.',
                'At the end of an adventure, you can retry the last encounter or start over. The finished game is kept.',
              ],
            },
            {
              title: 'A phone cannot join the room',
              list: [
                'Check that it is on the same Wi-Fi, not on mobile data, and that the room is open.',
                'Allow the app through the Windows firewall on private networks (it is listed as Taleweaver, or as Node.js when run from the code).',
                'If the room offers several addresses, try another. Turn off any VPN.',
                'The GM’s PC must stay on, with the app running.',
              ],
            },
            {
              title: 'The app is wrong or stuck',
              list: [
                'First look for a question waiting, in the turn panel or below the story.',
                'Undo, or use [[comfort.tools.correct]].',
                'If a page shows a loading error, check that the app’s server is running, then reload. The game is untouched.',
                'As a last resort, play the disputed point by the book, then enter the result.',
              ],
            },
          ],
        },
      ],
    },
  ],
}
