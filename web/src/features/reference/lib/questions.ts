import type { LocalizedText } from '@/shared/lib/localized'

export const themes = ['turn', 'move', 'combat', 'health', 'enemies', 'items', 'encounter'] as const
export type Theme = (typeof themes)[number]

/**
 * A question of the table and where the book answers it. No answer is written here: the passage of the book is the
 * answer. `find`: the first words, or some words, of the passage to show.
 */
export interface Question {
  theme: Theme
  text: LocalizedText
  chapter: string
  section: string
  find?: string
}

const ask = (theme: Theme, at: string, fr: string, en: string, find?: string): Question => {
  const [chapter = '', section = ''] = at.split('/')
  return { theme, chapter, section, find, text: { fr, en } }
}

export const questions: Question[] = [
  ask('turn', '07/The Initiative Track', 'Qui joue, et dans quel ordre ?', 'Who plays, and in which order?'),
  ask('turn', '07/Phases of an Adventurer’s Turn', 'Comment se déroule un tour ?', 'How does a turn go?'),
  ask(
    'turn',
    '07/Phases of an Adventurer’s Turn',
    'Gagne-t-on des SP au premier tour ?',
    'Do you gain SP on the first turn?',
    '1st',
  ),
  ask(
    'turn',
    '07/Standard Actions and Abilities',
    'Quelles actions, et à quel coût en SP ?',
    'Which actions, at what SP cost?',
  ),
  ask(
    'turn',
    '07/Exhaust and Flip',
    'Quelle différence entre épuiser et retourner ?',
    'Exhaust or flip: what is the difference?',
  ),
  ask(
    'turn',
    '09/Simultaneous Abilities',
    'Deux capacités en même temps : laquelle d’abord ?',
    'Two abilities at the same time: which first?',
  ),
  ask('move', '07/The Move Action', 'De combien peut-on se déplacer ?', 'How far can you move?'),
  ask('move', '07/Diagonals', 'Comment comptent les diagonales ?', 'How do diagonals work?'),
  ask(
    'move',
    '07/Break Attacks',
    'Quand quitter le contact provoque-t-il une attaque ?',
    'When does leaving provoke a Break Attack?',
  ),
  ask('move', '09/Jumping', 'Comment sauter par-dessus des cases ?', 'How do you jump over spaces?'),
  ask('move', '05/Terrain Types', 'Que fait chaque type de terrain ?', 'What does each terrain do?'),
  ask('move', '05/Elevation', 'Comment fonctionne l’élévation ?', 'How does elevation work?'),
  ask(
    'move',
    '07/Push, Pull, and Command',
    'Comment pousser, tirer ou commander une figurine ?',
    'How do Push, Pull and Command work?',
  ),
  ask(
    'combat',
    '07/Steps for Making an Attack',
    'Quelles sont les étapes d’une attaque ?',
    'What are the steps of an attack?',
  ),
  ask('combat', '07/How to Count Range', 'Comment compter la portée ?', 'How is range counted?'),
  ask('combat', '07/Line of Sight', 'Qu’est-ce qui bloque la ligne de vue ?', 'What blocks line of sight?'),
  ask(
    'combat',
    '07/Flanking and Backstab',
    'Quand y a-t-il prise en tenaille ou attaque dans le dos ?',
    'When is there flanking or backstab?',
  ),
  ask('combat', '07/Dodge', 'Comment esquiver ?', 'How do you dodge?'),
  ask('combat', '07/Counter', 'Quand peut-on riposter ?', 'When can you counter?'),
  ask('combat', '07/Empower', 'Comment renforcer une attaque ou un sort ?', 'How do you empower an attack or spell?'),
  ask(
    'combat',
    '07/Steps for Casting a Spell',
    'Comment lancer un sort et y résister ?',
    'How is a spell cast, and resisted?',
  ),
  ask('combat', '09/Re-Rolling Dice', 'Comment relancer des dés ?', 'How are dice re-rolled?'),
  ask('health', '07/Damage and Healing', 'Comment appliquer dégâts et soins ?', 'How are damage and healing applied?'),
  ask('health', '07/Damage Types', 'Quels dégâts l’armure réduit-elle ?', 'Which damage does armor reduce?'),
  ask('health', '09/Being Defeated', 'Que se passe-t-il quand on est vaincu ?', 'What happens when you are defeated?'),
  ask(
    'health',
    '09/Being Defeated',
    'Avec combien de PV revient un vaincu ?',
    'With how much HP do the defeated return?',
    'return to play',
  ),
  ask(
    'health',
    '09/Effects, Special Effects, & Ability Limitations',
    'Les effets et jetons se cumulent-ils ?',
    'Do effects and tokens stack?',
  ),
  ask('health', '11/Table 3: Effect Tokens', 'Que fait chaque effet ?', 'What does each effect do?'),
  ask('health', '09/Rounding Numbers', 'Dans quel sens arrondir ?', 'Which way do you round?'),
  ask(
    'enemies',
    '08/Intelligent Combatant Turns',
    'Comment joue un ennemi avec IA ?',
    'How does an intelligent combatant play?',
  ),
  ask('enemies', '08/Targeting Priority', 'Quelle cible un ennemi choisit-il ?', 'Which target does an enemy choose?'),
  ask(
    'enemies',
    '08/Golden Rules',
    'L’IA a plusieurs choix ou aucun : que faire ?',
    'The AI has several choices or none: what then?',
  ),
  ask(
    'enemies',
    '08/“Up To” vs “Within”',
    'Quelle différence entre « Up To » et « Within » ?',
    'Up To or Within: what is the difference?',
  ),
  ask(
    'enemies',
    '08/Activation Numbers and Groups',
    'Dans quel ordre joue un groupe d’ennemis ?',
    'In which order does a group play?',
  ),
  ask('enemies', '09/Large Base Figures', 'Comment se jouent les grands socles ?', 'How do large bases work?'),
  ask(
    'items',
    '07/The Re-Equip Action',
    'Peut-on changer d’équipement en combat ?',
    'Can you re-equip during an encounter?',
  ),
  ask('items', '11/Weapon Icons', 'Quelles armes peut-on porter ensemble ?', 'Which weapons go together?'),
  ask(
    'items',
    '09/Gaining Items During an Encounter',
    'Qui reçoit un objet gagné en rencontre ?',
    'Who gains an item found in an encounter?',
  ),
  ask('items', '06/Selling Items', 'À quel prix vendre un objet ?', 'What does an item sell for?'),
  ask('items', '06/Equipping Item Upgrades', 'Comment équiper une amélioration ?', 'How is an upgrade equipped?'),
  ask('encounter', '05/Setting up', 'Comment préparer une rencontre ?', 'How is an encounter set up?'),
  ask(
    'encounter',
    '05/End Conditions and Rewards',
    'Quand une rencontre se termine-t-elle ?',
    'When does an encounter end?',
  ),
  ask('encounter', '05/Urgency Tokens', 'À quoi servent les jetons Urgence ?', 'What do urgency tokens do?'),
  ask('encounter', '05/Totems', 'Comment fonctionne un totem ?', 'How does a totem work?'),
  ask('encounter', '05/Loot', 'Comment ramasser un butin ?', 'How is loot collected?'),
  ask('encounter', '05/Restoring Adventurers', 'Que récupère-t-on en restaurant ?', 'What does restoring give back?'),
  ask('encounter', '09/Adventurer Skill Checks', 'Comment faire un test de compétence ?', 'How is a skill check made?'),
]
